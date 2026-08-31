namespace USTHBStudy.Infrastructure.Services;

using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;

/// <inheritdoc />
public sealed class AccessControlService : IAccessControlService
{
    private readonly IDateTimeProvider _clock;

    public AccessControlService(IDateTimeProvider clock) => _clock = clock;

    public bool IsPremiumActive(AccessSubject subject) =>
        subject.IsActive
        && subject.IsPremiumFlag
        && subject.PremiumExpiresAt is { } expiresAt
        && expiresAt > _clock.UtcNow;

    public bool CanAccessPremiumContent(AccessSubject subject) => IsPremiumActive(subject);

    public void EnsureAccountActive(AccessSubject subject)
    {
        if (!subject.IsActive)
        {
            throw new ForbiddenAppException("This account is disabled.");
        }
    }

    public void EnsureCanAccess(bool isPremiumContent, AccessSubject subject)
    {
        EnsureAccountActive(subject);

        if (isPremiumContent && !IsPremiumActive(subject))
        {
            throw new ForbiddenAppException("This resource requires an active Premium subscription.");
        }
    }
}
