# Inbox Guardian — Comprehensive Feature Design

**Author:** Principal AI Product Architect
**Status:** Design Proposal

---

## 1. Executive Summary & Findings

### Core Objective
Inbox Guardian is the intelligent heart of OrvixFlow's customer communication strategy. It processes incoming multi-channel messages (primarily emails), understands intent, extracts a draft response using a sophisticated RAG pipeline, evaluates company-specific policies (Auto-Approve vs. Human Review), and hands execution off to an automation engine (n8n).

### Current State Assessment
After auditing the `memory` folder and current codebase, here is what exists and what is missing:

**What Already Exists:**
- **Infrastructure:** Multi-tenancy via EF Core Query Filters (`TenantId`), Hangfire background processing, and module gating (`[RequireModule("inbox-guardian")]`).
- **Core Entities:** `InboxEvent` (tracks ingested emails), `ActionRequest` (represents a manual review task), `WorkflowPolicy` (rules engine for auto-approval), and `AuditTrail`.
- **AI Services:** 
  - `IntentClassifierService`: Classifies intent into predefined categories and flags high-risk/spam emails.
  - `HybridVectorSearchService`: Retrieves context from tenant-specific `KnowledgeBase`.
  - `DraftGeneratorService`: Generates an email response with built-in prompt injection resistance and fallbacks for `INSUFFICIENT_CONTEXT`.
- **Workflow Pipeline:** `InboxEventsController` accepts webhooks and queues an `InboxProcessingJob`. The job calls `InboxGuardianService` (which binds the AI steps) and evaluates policies via `PolicyGateService`, finally triggering `WebhookCallbackService`.

**What is Missing / Needs Redesign:**
- **External Integration Strategy (n8n):** The boundary between OrvixFlow and n8n is undefined. n8n currently acts as a test webhook endpoint rather than a robust, tenant-aware integration layer.
- **Tenant Configuration:** There is no UI or backend mechanism for tenants to securely link their specific email accounts or configure routing rules dynamically.
- **Queue/Review UI:** The backend creates `ActionRequest`s, but the product requirements for how users interact with, approve, or reject these drafts in the frontend are incomplete.
- **Feedback Loop:** When humans edit a draft from an `ActionRequest`, the system needs a feedback mechanism to improve the RAG/Prompts over time.

---

## 2. Feature Scope & Boundaries

### What Inbox Guardian Is
A centralized **Intelligence and Orchestration Engine**. It is responsible solely for cognitive tasks: categorizing messages, fetching knowledge, drafting contextual replies, and applying business policies.

### Where It Starts and Ends
- **Starts:** When a raw message payload is ingested via the `/api/v1/inbox/events` POST endpoint. 
- **Ends:** When an asynchronous callback is fired to an external webhook (e.g., n8n) containing a structured response action (`draft_ready`, `human_review_required`, `spam_detected`), OR when a human approves an `ActionRequest` in the UI which subsequently fires the delivery callback.

### The Boundary: Platform vs. External Automation (n8n)
- **Inside OrvixFlow Platform:** Knowledge storage, semantic search, AI intent/draft generation, tenant policy evaluation, approval queues (the "Brain").
- **External (n8n/Integrations):** Email protocol handling (IMAP, Gmail/Outlook API, SMTP), alerting (Slack/Teams), ticketing (Zendesk API). OrvixFlow should **not** recreate IMAP/SMTP clients. 

---

## 3. The n8n Architecture Recommendation

### The Question
How should n8n integrate, and what is the tenant deployment model?

### Options Analyzed

1. **Shared Workflow for All Customers**
   - *Pros:* Easiest to maintain. One workflow to update.
   - *Cons:* Customer email credentials (IMAP/SMTP) vary natively. A shared workflow cannot easily handle hundreds of different IMAP connections without heavy custom credential looping or polling, which n8n is not optimized for in a single workflow. Furthermore, tenant isolation is risky.
   
2. **Heavy Logic in n8n**
   - *Pros:* Visual builder for all logic.
   - *Cons:* Duplicates logic already perfectly handled in C# (RAG, Policy Gates, Multi-tenancy). Hard to source-control, hard to tie into the OrvixFlow frontend UI (which needs to read `ActionRequest` DB tables).
   
3. **Hybrid Model: OrvixFlow as the Brain, n8n as a Programmatic Connector**
   - *Model:* Keep AI and Policy in C#. Use the n8n API to auto-provision a **tiny, dedicated n8n workflow per tenant per mailbox**. 
   - *Pros:* Total tenant isolation for credentials. Scales horizontally. OrvixFlow remains the single source of truth.

### The Recommendation: The Spoke-and-Hub Model
Inbox Guardian will minimize n8n logic and act as the core workflow engine. 
For n8n, we will use a **Template-Provisioned Workflow Per Customer (Hybrid)**.

**Justification:** 
Since a tenant (company/org) can have multiple users, and each user can connect their own mailboxes (or shared mailboxes), handling incoming/outgoing email requires **user-specific and mailbox-specific credentials** (OAuth for Gmail/M365). Because we are self-hosting a multi-tenant embedded version of n8n, OrvixFlow has full administrative API access. 
OrvixFlow will use the n8n API (`/api/v1/workflows`) to clone a master "Email Sync" template workflow for *each connected mailbox*. 
This mailbox-specific workflow does only two things:
1. Listens to their specific inbox (Trigger) → POSTs to OrvixFlow `/api/v1/inbox/events`.
2. Listens to an OrvixFlow webhook (Trigger) → Sends via their specific SMTP (Action).

This separates connection identity (n8n) from business intelligence (OrvixFlow), achieving infinite scalability and strict isolation.

---

## 4. Operational Flow

1. **Ingestion (Event-Driven):** 
   A tenant's n8n workflow receives an email and pushes it to `/api/v1/inbox/events`. The payload includes `SenderEmail`, `Subject`, `Body`, and `WebhookCallbackPath`.
2. **Deduplication:** 
   `InboxEventsController` verifies `MessageId` idempotency.
3. **Queuing:** 
   Hangfire dequeues the `InboxProcessingJob`.
4. **Cognitive Phase (`InboxGuardianService`):**
   - **Sanitization & Intent:** `IntentClassifierService` categorizes the email. Spams and highly escalated emails are immediately routed to a terminal state.
   - **RAG Retrieval:** `HybridVectorSearchService` grabs top snippets.
   - **Generation:** `DraftGeneratorService` generates the reply, applying Fallback logic if Context is insufficient.
5. **Policy Gate Phase (`PolicyGateService`):**
   - Evaluates the tenant's `WorkflowPolicy`. E.g., *"If Category=Sales AND Confidence > 0.9, Auto-Execute. Else, HoldForApproval."*
6. **Execution Phase:**
   - **Auto-Execute:** System updates DB status and fires the `WebhookCallbackService` to the tenant's n8n webhook to send the email immediately.
   - **HoldForApproval:** System creates an `ActionRequest`. A notification (via n8n Slack node or in-app) is sent to the Company Admin. The execution pauses.
7. **Human Review (UI):**
   A user logs into `/inbox/pending`, edits the `ActionRequest` draft, and clicks "Approve & Send". The Backend updates the `ActionRequest`, marks `InboxEvent` as `Completed`, and fires the `WebhookCallbackService` with the newly approved text.

---

## 5. Multi-Tenant & Customer Handling

- **Data Isolation:** Enforced natively via EF Core `HasQueryFilter`.
- **Tenant Rules & Prompts:** `WorkflowPolicy` entities are bound by `TenantId`. In Phase 2, we will introduce `AgentPersona` entities which allow tenants to override default instructions (tone, custom sign-offs) injected into the semantic kernel prompt.
- **Connections (Per-User/Per-Mailbox):** In the web UI, users can authenticate and connect their own features, including individual or shared mailboxes. A single company can therefore maintain multiple mailbox connections (mapped to specific users or teams). Under the hood, the OrvixFlow backend uses its embedded n8n API access to auto-provision a dedicated n8n workflow for each connected mailbox and safely stores their OAuth tokens in n8n's encrypted vault.

---

## 6. AI Behavior & Guardrails

- **Classification Categories:** Support, Sales, Billing, Feedback, Escalation, Spam.
- **Human Review Triggers:** High-risk keywords (legal, refund, IRS), low confidence score (< 0.7), unrecognized domains, or explicit tenant workflow policies.
- **Refusing Automation:** PII/PCI detection. If credit card numbers are detected in the draft, a hard stop is triggered.
- **Image/File Handling:** As defined in the `rag-extension-design.md`, an `ImageResolver` will retrieve relevant knowledge base images. The LLM marks insertion points using `[image:N]`.

---

## 7. Product & Admin Experience (UI)

### End-User (Tenant) Pages
- **/inbox/pending:** The primary queue. A split pane view showing the original email on the left, and the AI's classification, sources (RAG snippets), and editable draft on the right.
- **/inbox/history:** Read-only log of all processed messages, showing AI confidence and whether it was auto-sent or human-touched.
- **/settings/inbox (or User Profile settings):** 
  - **Connections:** "Connect Gmail", "Connect Outlook" buttons presented to individual users (triggers n8n OAuth flows embedded via UI). Users can authorize multiple mailboxes under their company.
  - **Identities:** Set the AI Persona tone (e.g., Casual, Professional, Technical).
  - **Policies:** A simple UI to define `WorkflowPolicy` rules (e.g., Toggle: "Auto-respond to general Support queries if confidence is > 95%").

### Super Admin Pages
- **/admin/inbox-metrics:** Global view of tokens used by Inbox Guardian, processing latency, and failure rates.
- **/admin/companies/{id}/inbox:** Override specific module toggles or examine stuck `ActionRequest`s.

---

## 8. Data & Domain Model Extensions

The underlying model is largely in place. Necessary improvements:

```csharp
// Extend existing WorkflowPolicy
public class WorkflowPolicy 
{
    // existing fields...
    public string CategoryMatch { get; set; } // e.g., "Sales,Support"
    public decimal MinimumConfidenceScore { get; set; } // e.g., 0.85
    public bool AutoExecuteAllowed { get; set; }
}

// New Entity: MailboxConnection (Maps a connected email to a User and n8n Workflow)
public class MailboxConnection
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string EmailAddress { get; set; }
    public string N8nWorkflowId { get; set; }
    public string N8nCredentialId { get; set; }
    public bool IsActive { get; set; }
}

// New Entity: FeedbackLoop (For reinforcing future drafts based on human edits)
public class DraftFeedback
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActionRequestId { get; set; }
    public string OriginalDraft { get; set; }
    public string FinalHumanDraft { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## 9. System Architecture & Modules

**Modularity:** InboxGuardian sits strictly inside the `OrvixFlow.Infrastructure/Ai` layer and is surfaced via the `OrvixFlow.Api` module.
**Reliability:** 
- Hangfire is crucial here. API should never block on LLM generation. 
- Retry policies: If the n8n webhook fails (`WebhookCallbackService`), Hangfire must retry the callback job with exponential backoff.
- Secret Management: Keys to third parties must leverage Azure KeyVault / AWS SecretsManager / or n8n Credentials Vault, never stored in plain text in the PostgreSQL DB.

---

## 10. Security & Access Model

- **Mailbox Credentials:** Managed primarily by n8n's secure credential system. OrvixFlow does not store raw passwords/tokens for user email accounts, maintaining zero-trust architecture.
- **Tenant Isolation:** Enforced via `ITenantProvider` and `BackgroundTenantProvider`.
- **RBAC:** 
  - `CompanyOwner` / `CompanyAdmin`: Can configure Inbox settings and connect emails.
  - `Manager` / `Member`: Can only review and approve `ActionRequest` drafts in the queue.
- **Audit Trails:** The existing `AuditTrail` DB intercepts every state transition (Ingest -> Pending -> Approved/Sent).

---

## 11. Rollout Approach

### Phase 1: Guided Copilot (MVP)
- **Goal:** AI writes drafts, humans send all of them.
- All incoming emails generate an `ActionRequest` (no auto-send policies).
- Basic UI under `/inbox/pending` for users to approve emails.
- Webhook trigger to a static shared n8n endpoint for testing.

### Phase 2: Trusted Autonomy
- Introduce `WorkflowPolicy` UI settings allowing tenants to enable Auto-Send for high-confidence intents.
- Implement the "Template Provisioned" n8n architecture to support diverse tenant IMAP/SMTP seamlessly.

### Phase 3: Learning Engine
- Introduce the `DraftFeedback` loop. Calculate edit-distance between the AI's draft and the human's final edit.
- If edit distance > threshold, trigger a background task to extract a new "guideline" to add to the tenant's KnowledgeBase or Persona instructions.

---

## 12. Testing and Production Readiness

- **Unit Tests:** `IntentClassifierServiceTests` and `PolicyGateServiceTests` must achieve 90% coverage on branch logic to ensure an email never auto-sends without strict validation.
- **Integration Tests:** End-to-end tests from HTTP POST `/events` -> Hangfire -> Semantic Kernel Mock -> DB state update.
- **Load Testing:** Validate that Hangfire queues do not overwhelm the Postgres DB connection limits or rate-limit the chosen LLM provider API under surge loads (e.g. marketing blasts resulting in hundreds of replies).
- **Diagnostics:** Utilize Serilog. Correlate logs passing `MessageId` and `TraceId` through the entire pipeline so an admin can trace a specific email's journey from ingestion to webhook callback.
