namespace USTHBStudy.Application.Notifications;

using USTHBStudy.Domain.Admin;

public interface INotificationService
{
    Task NotifyAsync(
        Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken ct = default);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary><paramref name="notificationId"/> null marks every notification read.</summary>
    Task MarkReadAsync(Guid userId, Guid? notificationId, CancellationToken ct = default);
}

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Link,
    bool IsRead,
    DateTime CreatedAt);
