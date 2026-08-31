namespace USTHBStudy.Infrastructure.Students;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Students;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Domain.Students;
using USTHBStudy.Infrastructure.Persistence;

public sealed class FavoriteService : IFavoriteService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public FavoriteService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<FavoriteDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var favorites = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        if (favorites.Count == 0)
        {
            return Array.Empty<FavoriteDto>();
        }

        var moduleIds = favorites.Where(f => f.Kind == FavoriteKind.Module).Select(f => f.EntityId).ToArray();
        var documentIds = favorites.Where(f => f.Kind == FavoriteKind.Document).Select(f => f.EntityId).ToArray();

        var modules = await _db.Modules.AsNoTracking()
            .Where(m => moduleIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name, m.Slug })
            .ToDictionaryAsync(m => m.Id, ct);

        var documents = await _db.Documents.AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Title, d.Slug, d.IsPremium })
            .ToDictionaryAsync(d => d.Id, ct);

        var result = new List<FavoriteDto>(favorites.Count);
        foreach (var favorite in favorites)
        {
            if (favorite.Kind == FavoriteKind.Module && modules.TryGetValue(favorite.EntityId, out var m))
            {
                result.Add(new FavoriteDto(favorite.Id, "Module", favorite.EntityId, m.Name, m.Slug, false, favorite.CreatedAt));
            }
            else if (favorite.Kind == FavoriteKind.Document && documents.TryGetValue(favorite.EntityId, out var d))
            {
                result.Add(new FavoriteDto(favorite.Id, "Document", favorite.EntityId, d.Title, d.Slug, d.IsPremium, favorite.CreatedAt));
            }
        }

        return result;
    }

    public async Task<FavoriteDto> AddAsync(Guid userId, FavoriteKind kind, Guid entityId, CancellationToken ct = default)
    {
        await EnsureTargetExistsAsync(kind, entityId, ct);

        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Kind == kind && f.EntityId == entityId, ct);

        if (existing is null)
        {
            existing = new Favorite
            {
                UserId = userId,
                Kind = kind,
                EntityId = entityId,
                CreatedAt = _clock.UtcNow,
            };
            _db.Favorites.Add(existing);
            await _db.SaveChangesAsync(ct);
        }

        return (await ListAsync(userId, ct)).First(f => f.Id == existing.Id);
    }

    public async Task RemoveAsync(Guid userId, FavoriteKind kind, Guid entityId, CancellationToken ct = default)
    {
        await _db.Favorites
            .Where(f => f.UserId == userId && f.Kind == kind && f.EntityId == entityId)
            .ExecuteDeleteAsync(ct);
    }

    private async Task EnsureTargetExistsAsync(FavoriteKind kind, Guid entityId, CancellationToken ct)
    {
        var exists = kind switch
        {
            FavoriteKind.Module => await _db.Modules.AnyAsync(m => m.Id == entityId, ct),
            FavoriteKind.Document => await _db.Documents.AnyAsync(
                d => d.Id == entityId && d.Status == DocumentStatus.Published, ct),
            _ => false,
        };

        if (!exists)
        {
            throw new NotFoundException(kind.ToString(), entityId);
        }
    }
}
