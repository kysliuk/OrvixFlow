# Stripe Setup Guide

**Author:** OrvixFlow
**Date:** 2026-04-16
**Purpose:** Guide for setting up Stripe integration in test mode

---

## Prerequisites

- Stripe test account (free at https://dashboard.stripe.com/test/apikeys)
- Access to OrvixFlow `.env` configuration

---

## Step 1: Create Stripe Test Account

1. Go to https://dashboard.stripe.com/test/apikeys
2. If you don't have an account, click "Sign up" and create one
3. Test mode is enabled by default (toggle in top-right shows "Test mode")

---

## Step 2: Get API Keys

1. Navigate to https://dashboard.stripe.com/test/apikeys
2. Copy the **Secret key** (starts with `sk_test_`)
3. Add to `.env`:
   ```
   Stripe__SecretKey=sk_test_your_actual_key_here
   ```

---

## Step 3: Create Products and Prices

For each plan (Starter, Growth, Business), create a product and recurring price:

### 3.1 Create Starter Product

1. Go to https://dashboard.stripe.com/test/products
2. Click **+ Add product**
3. Fill in:
   - **Name:** OrvixFlow Starter
   - **Pricing model:** Standard pricing
   - **Price:** $29.00 USD / month
4. Click **Save product**
5. Copy the **Price ID** (starts with `price_`)
6. Repeat for yearly: $290.00 USD / year

### 3.2 Create Growth Product

1. **Name:** OrvixFlow Growth
2. **Price:** $99.00 USD / month (or $990 / year)

### 3.3 Create Business Product

1. **Name:** OrvixFlow Business
2. **Price:** $299.00 USD / month (or $2,990 / year)

### 3.4 Update .env with Price IDs

```bash
Stripe__Prices__Starter__Monthly=price_xxx
Stripe__Prices__Starter__Yearly=price_yyy
Stripe__Prices__Growth__Monthly=price_xxx
Stripe__Prices__Growth__Yearly=price_yyy
Stripe__Prices__Business__Monthly=price_xxx
Stripe__Prices__Business__Yearly=price_yyy
```

---

## Step 4: Configure Webhook

### 4.1 Get Webhook Secret

1. Go to https://dashboard.stripe.com/test/webhooks
2. Click **+ Add endpoint**
3. Configure:
   - **Endpoint URL:** `https://your-domain.com/api/billing/stripe/webhook`
   - **Listen to events:** Select all events, or manually add:
     - `invoice.paid`
     - `invoice.payment_failed`
     - `customer.subscription.updated`
4. Click **Add endpoint**
5. Copy the **Signing secret** (starts with `whsec_`)

### 4.2 Add Webhook Secret to .env

```bash
Stripe__WebhookSecret=whsec_your_actual_secret_here
```

### 4.3 Local Development (Stripe CLI)

For local testing without deploying:

1. Install Stripe CLI: https://stripe.com/docs/stripe-cli
2. Login: `stripe login`
3. Forward webhooks:
   ```bash
   stripe listen --forward-to localhost:5000/api/billing/stripe/webhook
   ```
4. Copy the output webhook signing secret to `.env`:
   ```
   Stripe__WebhookSecret=whsec_xxx
   ```
5. Trigger test events:
   ```bash
   stripe trigger invoice.paid
   ```

---

## Step 5: Test Card Numbers

Use these test cards in checkout:

| Card Number | Scenario |
|-------------|----------|
| `4242 4242 4242 4242` | Successful payment |
| `4000 0025 0000 3155` | Requires 3D Secure |
| `4000 0000 0000 9995` | Insufficient funds |
| `4100 0000 0000 0019` | Always fails |

Use any future expiry date and any 3-digit CVC.

---

## Step 6: Verify Integration

### Test Checkout Flow

1. Start the API: `dotnet run --project OrvixFlow.Api`
2. Create a test company or use existing
3. Call checkout endpoint:
   ```bash
   curl -X POST http://localhost:5000/api/billing/checkout \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer YOUR_JWT" \
     -d '{"planTemplateId": "YOUR-PLAN-GUID"}'
   ```
4. Open the returned `checkoutUrl` in browser
5. Complete payment with test card
6. Verify webhook was received and invoice created

### Test Webhook Locally

```bash
# Test invoice.paid event
stripe trigger invoice.paid

# Test payment failure
stripe trigger invoice.payment_failed
```

---

## Troubleshooting

### Webhook Not Received

1. Check Stripe dashboard for webhook logs: https://dashboard.stripe.com/test/webhooks
2. Verify the webhook URL is correct and accessible
3. Check application logs for webhook processing errors

### Signature Validation Failed

1. Ensure `Stripe__WebhookSecret` is correctly set
2. For local dev, use Stripe CLI forwarding (not ngrok)
3. Check that raw request body is being read (not model-bound)

### Checkout Session Fails

1. Verify price IDs are correctly configured
2. Check `Stripe__SecretKey` is set
3. Ensure price IDs match your Stripe account (test vs live)

---

## Environment Variables Summary

```bash
# Required
Stripe__SecretKey=sk_test_xxx
Stripe__WebhookSecret=whsec_xxx

# Price IDs (create in Stripe Dashboard)
Stripe__Prices__Starter__Monthly=price_xxx
Stripe__Prices__Starter__Yearly=price_xxx
Stripe__Prices__Growth__Monthly=price_xxx
Stripe__Prices__Growth__Yearly=price_xxx
Stripe__Prices__Business__Monthly=price_xxx
Stripe__Prices__Business__Yearly=price_xxx
```

---

## Moving to Production

When ready to go live:

1. Switch from test to live mode in Stripe dashboard
2. Get live API keys: https://dashboard.stripe.com/apikeys
3. Update `.env`:
   ```bash
   Stripe__SecretKey=sk_live_xxx
   Stripe__WebhookSecret=whsec_xxx
   ```
4. Create new products/prices in live mode
5. Update price IDs
6. Update webhook endpoint URL to production domain

---

## References

- Stripe Test Cards: https://stripe.com/docs/testing
- Stripe CLI: https://stripe.com/docs/stripe-cli
- Webhook Events: https://stripe.com/docs/webhooks/stripe-events
