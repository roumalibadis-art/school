namespace USTHBStudy.Application.Abstractions;

/// <summary>
/// The single decision point for Premium / account-state access rules (PRD §23).
/// Never inline <c>if (user.IsPremium)</c> anywhere else. Document-level checks are added
/// here in Phase 3/5; Phase 1 establishes the Premium-active rule (PRD §24).
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// True only if the account is active, flagged Premium, and the expiry is in the future.
    /// A past <c>PremiumExpiresAt</c> means the user is treated as free (PRD §24/§61-#5).
    /// </summary>
    bool IsPremiumActive(AccessSubject subject);

    /// <summary>True if the subject may open Premium-gated content right now.</summary>
    bool CanAccessPremiumContent(AccessSubject subject);

    /// <summary>Throws <see cref="Common.ForbiddenAppException"/> if the account is disabled (PRD §61-#4).</summary>
    void EnsureAccountActive(AccessSubject subject);
}

/// <summary>Minimal account facts needed for an access decision — keeps this port free of entity types.</summary>
public sealed record AccessSubject(bool IsActive, bool IsPremiumFlag, DateTime? PremiumExpiresAt);
