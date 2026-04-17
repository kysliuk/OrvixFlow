using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Invoice status for billing records.
/// Stored as string in DB via EF value converter (same pattern as SubscriptionState).
/// T4-3: Converted from static class to enum per L1 lesson.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    Open = 1,
    Paid = 2,
    Void = 3,
    Uncollectible = 4
}

/// <summary>
/// Extension methods for InvoiceStatus enum.
/// T4-3: Added with enum conversion.
/// </summary>
public static class InvoiceStatusExtensions
{
    /// <summary>Parse a string from DB into InvoiceStatus enum.</summary>
    public static InvoiceStatus ParseStatus(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out InvoiceStatus result)
            ? result
            : InvoiceStatus.Draft;

    /// <summary>Serialize to canonical string for DB storage.</summary>
    public static string ToClaimValue(this InvoiceStatus status) => status.ToString();
}

/// <summary>
/// Invoice record for tracking payments.
/// Populated from Stripe webhook events (invoice.paid, invoice.payment_failed).
/// </summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid CompanyId { get; set; }
    
    /// <summary>
    /// External Stripe invoice ID.
    /// </summary>
    public string ExternalInvoiceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Invoice amount in cents.
    /// </summary>
    public int AmountCents { get; set; }
    
    /// <summary>
    /// Currency code (e.g., "USD").
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Invoice status: Draft, Open, Paid, Void, Uncollectible.
    /// T4-3: Changed from string to InvoiceStatus enum.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    
    /// <summary>
    /// Invoice PDF URL from Stripe.
    /// </summary>
    public string? InvoicePdfUrl { get; set; }
    
    /// <summary>
    /// Invoice hosted URL from Stripe.
    /// </summary>
    public string? InvoiceUrl { get; set; }
    
    /// <summary>
    /// Billing period start.
    /// </summary>
    public DateTime PeriodStart { get; set; }
    
    /// <summary>
    /// Billing period end.
    /// </summary>
    public DateTime PeriodEnd { get; set; }
    
    /// <summary>
    /// When the invoice was finalized.
    /// </summary>
    public DateTime? FinalizedAt { get; set; }
    
    /// <summary>
    /// When the invoice was paid.
    /// </summary>
    public DateTime? PaidAt { get; set; }
    
    /// <summary>
    /// When the invoice was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Tenant Company { get; set; } = null!;
}
