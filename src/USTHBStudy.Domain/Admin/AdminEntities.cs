namespace USTHBStudy.Domain.Admin;

using USTHBStudy.Domain.Common;

/// <summary>An administrative action record (PRD §42). No secrets/tokens are ever stored here.</summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; set; }
    public string? ActorEmail { get; set; }

    /// <summary>Dotted action key, e.g. <c>user.suspended</c>, <c>document.published</c>, <c>payment.approved</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>Small JSON blob with the relevant before/after fields.</summary>
    public string? Metadata { get; set; }

    public string? IpAddress { get; set; }

    public DateTime OccurredAt { get; set; }
}

public enum NotificationType
{
    ContributionApproved = 1,
    ContributionRejected = 2,
    SubscriptionActivated = 3,
    SubscriptionExpiring = 4,
    SubscriptionExpired = 5,
    ReportResolved = 6,
    NewDocument = 7,
}

/// <summary>An in-app notification (PRD §41). Email delivery is a separate provider added later.</summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
