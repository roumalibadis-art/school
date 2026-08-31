namespace USTHBStudy.Infrastructure.Academic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Academic;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Common;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// Shared CRUD implementation for academic-hierarchy nodes (PRD §34). Concrete services supply
/// mapping, the parent filter, and how an input becomes / updates an entity. Search-free list
/// queries (dependent dropdowns) are cached and invalidated on any academic write (PRD §56).
/// </summary>
public abstract class AcademicNodeService<TEntity, TDto, TInput> : IAcademicNodeService<TDto, TInput>
    where TEntity : AcademicEntity
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly AcademicCacheSignal _cacheSignal;

    protected AcademicNodeService(AcademicServiceDependencies deps)
    {
        Db = deps.Db;
        Clock = deps.Clock;
        _cache = deps.Cache;
        _cacheSignal = deps.CacheSignal;
    }

    protected AppDbContext Db { get; }

    protected IDateTimeProvider Clock { get; }

    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    protected string EntityName => typeof(TEntity).Name;

    // ---- hooks ----
    protected abstract TDto Map(TEntity entity);

    protected abstract Task<TEntity> BuildAsync(TInput input, CancellationToken ct);

    protected abstract Task ApplyAsync(TInput input, TEntity entity, CancellationToken ct);

    protected virtual IQueryable<TEntity> ApplyParentFilter(IQueryable<TEntity> query, Guid parentId) => query;

    protected virtual IQueryable<TEntity> DefaultOrder(IQueryable<TEntity> query) => query.OrderBy(e => e.Name);

    // ---- CRUD ----
    public async Task<PagedResult<TDto>> ListAsync(AcademicQuery query, CancellationToken ct = default)
    {
        // Cache only the deterministic reference-data case (no free-text search).
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var key = $"academic:{EntityName}:{query.ParentId}:{query.IncludeInactive}:" +
                      $"{query.Page}:{query.PageSize}:v{_cacheSignal.Version}";

            if (_cache.TryGetValue(key, out PagedResult<TDto>? cached) && cached is not null)
            {
                return cached;
            }

            var fresh = await QueryPageAsync(query, ct);
            _cache.Set(key, fresh, CacheTtl);
            return fresh;
        }

        return await QueryPageAsync(query, ct);
    }

    private async Task<PagedResult<TDto>> QueryPageAsync(AcademicQuery query, CancellationToken ct)
    {
        var paging = new PaginationParams { Page = query.Page, PageSize = query.PageSize };

        var q = Set.AsNoTracking();
        if (query.ParentId is { } parentId)
        {
            q = ApplyParentFilter(q, parentId);
        }

        if (!query.IncludeInactive)
        {
            q = q.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(e => EF.Functions.Like(e.Name, $"%{term}%"));
        }

        var total = await q.LongCountAsync(ct);
        var entities = await DefaultOrder(q).Skip(paging.Skip).Take(paging.Take).ToListAsync(ct);

        return new PagedResult<TDto>(entities.Select(Map).ToArray(), paging.Page, paging.PageSize, total);
    }

    public async Task<TDto> GetAsync(Guid id, CancellationToken ct = default) =>
        Map(await RequireAsync(id, ct));

    public async Task<TDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var entity = await Set.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug, ct)
                     ?? throw new NotFoundException(EntityName, slug);
        return Map(entity);
    }

    public async Task<TDto> CreateAsync(TInput input, CancellationToken ct = default)
    {
        var entity = await BuildAsync(input, ct);
        Set.Add(entity);
        await Db.SaveChangesAsync(ct);
        _cacheSignal.Bump();
        return Map(entity);
    }

    public async Task<TDto> UpdateAsync(Guid id, TInput input, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct);
        await ApplyAsync(input, entity, ct);
        await Db.SaveChangesAsync(ct);
        _cacheSignal.Bump();
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct);
        entity.IsDeleted = true;
        entity.DeletedAt = Clock.UtcNow;
        await Db.SaveChangesAsync(ct);
        _cacheSignal.Bump();
    }

    // ---- helpers for concrete services ----
    protected async Task<TEntity> RequireAsync(Guid id, CancellationToken ct) =>
        await Set.FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException(EntityName, id);

    protected async Task RequireExistsAsync<TParent>(Guid id, CancellationToken ct)
        where TParent : AcademicEntity
    {
        if (!await Db.Set<TParent>().AnyAsync(p => p.Id == id, ct))
        {
            throw new NotFoundException(typeof(TParent).Name, id);
        }
    }

    /// <summary>Sets Name and (re)generates a unique slug when the name changes or none exists yet.</summary>
    protected async Task SetNameAsync(TEntity entity, string name, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (entity.Slug.Length == 0 || !string.Equals(entity.Name, trimmed, StringComparison.Ordinal))
        {
            entity.Slug = await UniqueSlugAsync(trimmed, entity.Id, ct);
        }

        entity.Name = trimmed;
    }

    private async Task<string> UniqueSlugAsync(string text, Guid excludeId, CancellationToken ct)
    {
        var baseSlug = Slugifier.Slugify(text);
        if (baseSlug.Length == 0)
        {
            baseSlug = EntityName.ToLowerInvariant();
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await Set.IgnoreQueryFilters().AnyAsync(e => e.Slug == slug && e.Id != excludeId, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }
}
