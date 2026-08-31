namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Notifications;

[Route("api/me/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(INotificationService notifications, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> List(
        [FromQuery] bool unreadOnly = false, [FromQuery] int take = 30, CancellationToken ct = default) =>
        Ok(ApiResponse.Data(await _notifications.ListAsync(UserId, unreadOnly, take, ct)));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _notifications.UnreadCountAsync(UserId, ct)));

    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse>> MarkRead([FromQuery] Guid? id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(UserId, id, ct);
        return Ok(ApiResponse.Ok("Marqué comme lu."));
    }
}
