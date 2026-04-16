using System;

namespace OrvixFlow.Core.Entities;

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
    /// </summary>
    public string Status { get; set; } = "Draft";
    
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

/// <summary>
/// Invoice status constants.
/// </summary>
public static class InvoiceStatus
{
    public const string Draft = "Draft";
    public const string Open = "Open";
    public const string Paid = "Paid";
    public const string Void = "Void";
    public const string Uncollectible = "Uncollectible";
}
