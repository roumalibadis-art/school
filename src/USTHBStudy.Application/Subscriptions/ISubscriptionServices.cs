namespace USTHBStudy.Application.Subscriptions;

using USTHBStudy.Application.Common;

public interface ISubscriptionPlanService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken ct = default);

    Task<SubscriptionPlanDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<SubscriptionPlanDto> CreateAsync(SubscriptionPlanInput input, CancellationToken ct = default);

    Task<SubscriptionPlanDto> UpdateAsync(Guid id, SubscriptionPlanInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ISubscriptionService
{
    /// <summary>Starts checkout: a Pending subscription + Pending payment with instructions (PRD §25).</summary>
    Task<CheckoutResult> CheckoutAsync(Guid userId, Guid planId, CancellationToken ct = default);

    Task<MySubscriptionsDto> GetMineAsync(Guid userId, CancellationToken ct = default);

    // --- admin (PRD §25) ---
    Task<PagedResult<AdminPaymentDto>> ListPaymentsAsync(PaymentQuery query, CancellationToken ct = default);

    /// <summary>Marks a payment paid and activates / extends the user's Premium (PRD §24/§25).</summary>
    Task<AdminPaymentDto> ApprovePaymentAsync(Guid paymentId, string? note, CancellationToken ct = default);

    Task<AdminPaymentDto> RejectPaymentAsync(Guid paymentId, string? note, CancellationToken ct = default);

    Task<SubscriptionDto> ExtendSubscriptionAsync(Guid subscriptionId, int days, CancellationToken ct = default);
}
