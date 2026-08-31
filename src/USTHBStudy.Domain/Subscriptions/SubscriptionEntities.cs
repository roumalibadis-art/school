namespace USTHBStudy.Domain.Subscriptions;

using USTHBStudy.Domain.Common;

public enum SubscriptionStatus
{
    Pending = 1,
    Active = 2,
    Expired = 3,
    Cancelled = 4,
}

public enum PaymentStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
}

/// <summary>An admin-configurable subscription offer (PRD §22). Prices are never hard-coded in source.</summary>
public class SubscriptionPlan : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "DZD";

    /// <summary>One feature per line.</summary>
    public string? Features { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

/// <summary>A user's subscription to a plan for a period (PRD §22).</summary>
public class Subscription : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    /// <summary>Snapshots so later plan edits don't rewrite history.</summary>
    public int DurationDays { get; set; }
    public decimal PriceAtPurchase { get; set; }
    public string Currency { get; set; } = "DZD";

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>A payment record (PRD §26). No card / sensitive data is stored.</summary>
public class Payment : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "DZD";

    public string Provider { get; set; } = "manual";

    /// <summary>The code the student quotes when paying (CCP / BaridiMob / …).</summary>
    public string TransactionReference { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime? PaidAt { get; set; }
    public string? AdminNote { get; set; }
}
