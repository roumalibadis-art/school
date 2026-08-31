namespace USTHBStudy.Infrastructure.Notifications;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Notifications;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Infrastructure.Persistence;

public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public NotificationService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task NotifyAsync(
        Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Link = link,
            CreatedAt = _clock.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid userId, bool unreadOnly, int take, CancellationToken ct = default)
    {
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            q = q.Where(n => !n.IsRead);
        }

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid userId, Guid? notificationId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var q = _db.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (notificationId is { } id)
        {
            q = q.Where(n => n.Id == id);
        }

        await q.ExecuteUpdateAsync(s => s
            .SetProperty(n => n.IsRead, true)
            .SetProperty(n => n.ReadAt, now), ct);
    }
}
