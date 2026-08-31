namespace USTHBStudy.Infrastructure.Subscriptions;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Subscriptions;
using USTHBStudy.Domain.Subscriptions;
using USTHBStudy.Infrastructure.Persistence;

public sealed class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SubscriptionPlanService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.SubscriptionPlans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.DurationDays)
            .Select(p => Map(p))
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlanDto> GetAsync(Guid id, CancellationToken ct = default) =>
        Map(await Require(id, ct));

    public async Task<SubscriptionPlanDto> CreateAsync(SubscriptionPlanInput input, CancellationToken ct = default)
    {
        var plan = new SubscriptionPlan();
        Apply(input, plan);
        plan.Slug = await UniqueSlugAsync(input.Name, plan.Id, ct);
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return Map(plan);
    }

    public async Task<SubscriptionPlanDto> UpdateAsync(Guid id, SubscriptionPlanInput input, CancellationToken ct = default)
    {
        var plan = await Require(id, ct);
        if (!string.Equals(plan.Name, input.Name.Trim(), StringComparison.Ordinal))
        {
            plan.Slug = await UniqueSlugAsync(input.Name, plan.Id, ct);
        }

        Apply(input, plan);
        await _db.SaveChangesAsync(ct);
        return Map(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await Require(id, ct);
        plan.IsDeleted = true;
        plan.DeletedAt = _clock.UtcNow;
        plan.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(SubscriptionPlanInput input, SubscriptionPlan plan)
    {
        plan.Name = input.Name.Trim();
        plan.Description = input.Description?.Trim();
        plan.DurationDays = input.DurationDays;
        plan.Price = input.Price;
        plan.Currency = string.IsNullOrWhiteSpace(input.Currency) ? "DZD" : input.Currency.Trim().ToUpperInvariant();
        plan.Features = input.Features?.Trim();
        plan.IsActive = input.IsActive;
        plan.DisplayOrder = input.DisplayOrder;
    }

    private async Task<SubscriptionPlan> Require(Guid id, CancellationToken ct) =>
        await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("SubscriptionPlan", id);

    private async Task<string> UniqueSlugAsync(string name, Guid excludeId, CancellationToken ct)
    {
        var baseSlug = Slugifier.Slugify(name);
        if (baseSlug.Length == 0)
        {
            baseSlug = "plan";
        }

        var slug = baseSlug;
        var n = 2;
        while (await _db.SubscriptionPlans.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct))
        {
            slug = $"{baseSlug}-{n++}";
        }

        return slug;
    }

    internal static SubscriptionPlanDto Map(SubscriptionPlan p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.DurationDays, p.Price, p.Currency,
        SplitFeatures(p.Features), p.IsActive, p.DisplayOrder);

    internal static IReadOnlyList<string> SplitFeatures(string? features) =>
        string.IsNullOrWhiteSpace(features)
            ? Array.Empty<string>()
            : features.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
