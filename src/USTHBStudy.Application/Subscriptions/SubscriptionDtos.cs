namespace USTHBStudy.Application.Subscriptions;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int DurationDays,
    decimal Price,
    string Currency,
    IReadOnlyList<string> Features,
    bool IsActive,
    int DisplayOrder);

public sealed record SubscriptionPlanInput(
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    string? Currency,
    string? Features,
    bool IsActive = true,
    int DisplayOrder = 0);

public sealed record PaymentDto(
    Guid Id,
    Guid? SubscriptionId,
    decimal Amount,
    string Currency,
    string Provider,
    string Reference,
    string Status,
    DateTime? PaidAt,
    string? AdminNote,
    DateTime CreatedAt);

public sealed record SubscriptionDto(
    Guid Id,
    string PlanName,
    string Status,
    DateTime? StartsAt,
    DateTime? EndsAt,
    decimal PriceAtPurchase,
    string Currency,
    PaymentDto? LatestPayment);

public sealed record CreateSubscriptionRequest(Guid PlanId);

public sealed record CheckoutResult(
    SubscriptionDto Subscription,
    string Provider,
    string Reference,
    string Instructions);

public sealed record MySubscriptionsDto(
    SubscriptionDto? Current,
    bool IsPremiumActive,
    DateTime? PremiumExpiresAt,
    IReadOnlyList<SubscriptionDto> History);

public sealed record AdminPaymentDto(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string UserName,
    Guid? SubscriptionId,
    string? PlanName,
    decimal Amount,
    string Currency,
    string Provider,
    string Reference,
    string Status,
    DateTime? PaidAt,
    string? AdminNote,
    DateTime CreatedAt);

public sealed record ResolvePaymentRequest(string? Note);

public sealed record ExtendSubscriptionRequest(int Days);

public sealed record PaymentQuery(string? Status = null, int Page = 1, int PageSize = 20);
