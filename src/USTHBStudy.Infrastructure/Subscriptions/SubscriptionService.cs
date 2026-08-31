namespace USTHBStudy.Infrastructure.Subscriptions;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Notifications;
using USTHBStudy.Application.Subscriptions;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Domain.Subscriptions;
using USTHBStudy.Infrastructure.Persistence;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IAccessControlService _access;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;

    public SubscriptionService(
        AppDbContext db,
        IPaymentProvider paymentProvider,
        IAccessControlService access,
        INotificationService notifications,
        IAuditLogger audit,
        IDateTimeProvider clock)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _access = access;
        _notifications = notifications;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CheckoutResult> CheckoutAsync(Guid userId, Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
                   ?? throw new NotFoundException("SubscriptionPlan", planId);

        var pendingExists = await _db.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Pending, ct);
        if (pendingExists)
        {
            throw new ConflictException("You already have a subscription awaiting payment confirmation.");
        }

        var subscription = new Subscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Pending,
            DurationDays = plan.DurationDays,
            PriceAtPurchase = plan.Price,
            Currency = plan.Currency,
        };

        var payment = new Payment
        {
            UserId = userId,
            Subscription = subscription,
            Amount = plan.Price,
            Currency = plan.Currency,
            Provider = _paymentProvider.Key,
            TransactionReference = NewReference(),
            Status = PaymentStatus.Pending,
        };

        _db.Subscriptions.Add(subscription);
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        var initiation = await _paymentProvider.InitiateAsync(
            new PaymentInitiationRequest(payment.Id, userId, payment.Amount, payment.Currency, payment.TransactionReference, plan.Name),
            ct);

        return new CheckoutResult(
            ToDto(subscription, plan.Name, payment),
            initiation.Provider,
            initiation.Reference,
            initiation.Instructions);
    }

    public async Task<MySubscriptionsDto> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var subscriptions = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Plan)
            .Include(s => s.Payments)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);

        var dtos = subscriptions.Select(s => ToDto(s, s.Plan?.Name ?? "—", LatestPayment(s))).ToList();
        var current = dtos.FirstOrDefault(d => d.Status is "Active" or "Pending");

        var isActive = _access.IsPremiumActive(new AccessSubject(user.IsActive, user.IsPremium, user.PremiumExpiresAt));

        string? instructions = null;
        var pending = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Pending);
        if (pending is not null && LatestPayment(pending) is { Status: PaymentStatus.Pending } payment)
        {
            var initiation = await _paymentProvider.InitiateAsync(
                new PaymentInitiationRequest(
                    payment.Id, userId, payment.Amount, payment.Currency, payment.TransactionReference, pending.Plan?.Name ?? "—"),
                ct);
            instructions = initiation.Instructions;
        }

        return new MySubscriptionsDto(current, isActive, user.PremiumExpiresAt, instructions, dtos);
    }

    public async Task<PagedResult<AdminPaymentDto>> ListPaymentsAsync(PaymentQuery query, CancellationToken ct = default)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = _db.Payments.AsNoTracking();
        if (query.Status is not null && Enum.TryParse<PaymentStatus>(query.Status, true, out var status))
        {
            q = q.Where(p => p.Status == status);
        }

        var total = await q.LongCountAsync(ct);

        var rows = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip(paging.Skip).Take(paging.Take)
            .Select(p => new
            {
                Payment = p,
                User = _db.Users.Where(u => u.Id == p.UserId).Select(u => new { u.Email, u.FirstName, u.LastName }).FirstOrDefault(),
                PlanName = p.Subscription != null ? p.Subscription.Plan!.Name : null,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminPaymentDto(
            r.Payment.Id, r.Payment.UserId, r.User?.Email ?? "", $"{r.User?.FirstName} {r.User?.LastName}".Trim(),
            r.Payment.SubscriptionId, r.PlanName, r.Payment.Amount, r.Payment.Currency, r.Payment.Provider,
            r.Payment.TransactionReference, r.Payment.Status.ToString(), r.Payment.PaidAt, r.Payment.AdminNote,
            r.Payment.CreatedAt)).ToArray();

        return new PagedResult<AdminPaymentDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<AdminPaymentDto> ApprovePaymentAsync(Guid paymentId, string? note, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Subscription)!.ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new NotFoundException("Payment", paymentId);

        if (payment.Status == PaymentStatus.Success)
        {
            throw new ConflictException("This payment is already approved.");
        }

        var now = _clock.UtcNow;
        payment.Status = PaymentStatus.Success;
        payment.PaidAt = now;
        payment.AdminNote = note?.Trim();

        var subscription = payment.Subscription;
        if (subscription is not null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == payment.UserId, ct)
                       ?? throw new NotFoundException("User", payment.UserId);

            // Stack on top of any remaining Premium time (PRD §24).
            var startFrom = user.PremiumExpiresAt is { } exp && exp > now ? exp : now;
            subscription.Status = SubscriptionStatus.Active;
            subscription.StartsAt = now;
            subscription.EndsAt = startFrom.AddDays(subscription.DurationDays);

            user.IsPremium = true;
            user.PremiumExpiresAt = subscription.EndsAt;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("payment.approved", "Payment", payment.Id.ToString(),
            new { payment.Amount, payment.Currency, subscription?.EndsAt }, ct);
        if (subscription is not null)
        {
            await _notifications.NotifyAsync(payment.UserId, NotificationType.SubscriptionActivated,
                "Abonnement activé",
                $"Votre abonnement Premium est actif jusqu'au {subscription.EndsAt:dd/MM/yyyy}.",
                "/subscribe", ct);
        }

        return await GetAdminPaymentAsync(payment.Id, ct);
    }

    public async Task<AdminPaymentDto> RejectPaymentAsync(Guid paymentId, string? note, CancellationToken ct = default)
    {
        var payment = await _db.Payments.Include(p => p.Subscription)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new NotFoundException("Payment", paymentId);

        if (payment.Status == PaymentStatus.Success)
        {
            throw new ConflictException("An approved payment cannot be rejected — issue a refund instead.");
        }

        payment.Status = PaymentStatus.Failed;
        payment.AdminNote = note?.Trim();
        if (payment.Subscription is { Status: SubscriptionStatus.Pending } sub)
        {
            sub.Status = SubscriptionStatus.Cancelled;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("payment.rejected", "Payment", payment.Id.ToString(), new { note }, ct);
        return await GetAdminPaymentAsync(payment.Id, ct);
    }

    public async Task<SubscriptionDto> ExtendSubscriptionAsync(Guid subscriptionId, int days, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions.Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new NotFoundException("Subscription", subscriptionId);

        var now = _clock.UtcNow;
        var from = subscription.EndsAt is { } end && end > now ? end : now;
        subscription.EndsAt = from.AddDays(days);
        subscription.Status = SubscriptionStatus.Active;
        subscription.StartsAt ??= now;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == subscription.UserId, ct);
        if (user is not null && (user.PremiumExpiresAt is null || user.PremiumExpiresAt < subscription.EndsAt))
        {
            user.IsPremium = true;
            user.PremiumExpiresAt = subscription.EndsAt;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(subscription, subscription.Plan?.Name ?? "—", null);
    }

    private async Task<AdminPaymentDto> GetAdminPaymentAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.Payments.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Payment = p,
                User = _db.Users.Where(u => u.Id == p.UserId).Select(u => new { u.Email, u.FirstName, u.LastName }).FirstOrDefault(),
                PlanName = p.Subscription != null ? p.Subscription.Plan!.Name : null,
            })
            .FirstAsync(ct);

        return new AdminPaymentDto(
            row.Payment.Id, row.Payment.UserId, row.User?.Email ?? "", $"{row.User?.FirstName} {row.User?.LastName}".Trim(),
            row.Payment.SubscriptionId, row.PlanName, row.Payment.Amount, row.Payment.Currency, row.Payment.Provider,
            row.Payment.TransactionReference, row.Payment.Status.ToString(), row.Payment.PaidAt, row.Payment.AdminNote,
            row.Payment.CreatedAt);
    }

    private static Payment? LatestPayment(Subscription s) =>
        s.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

    private static SubscriptionDto ToDto(Subscription s, string planName, Payment? payment) => new(
        s.Id, planName, s.Status.ToString(), s.StartsAt, s.EndsAt, s.PriceAtPurchase, s.Currency,
        payment is null ? null : new PaymentDto(
            payment.Id, payment.SubscriptionId, payment.Amount, payment.Currency, payment.Provider,
            payment.TransactionReference, payment.Status.ToString(), payment.PaidAt, payment.AdminNote, payment.CreatedAt));

    private static string NewReference()
    {
        Span<byte> bytes = stackalloc byte[5];
        RandomNumberGenerator.Fill(bytes);
        return $"USTHB-{Convert.ToHexString(bytes)}";
    }
}
