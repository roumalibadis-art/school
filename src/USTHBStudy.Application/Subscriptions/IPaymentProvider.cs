namespace USTHBStudy.Application.Subscriptions;

/// <summary>
/// A payment provider (PRD §25). The MVP ships only <c>manual</c> — it returns instructions and the
/// reference the student quotes; an admin verifies and approves. Automated providers (webhook-driven)
/// implement the same contract later. Premium is NEVER activated on a client-reported success.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stable key, e.g. <c>manual</c>.</summary>
    string Key { get; }

    Task<PaymentInitiation> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct = default);
}

public sealed record PaymentInitiationRequest(
    Guid PaymentId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Reference,
    string PlanName);

public sealed record PaymentInitiation(string Provider, string Reference, string Instructions);
