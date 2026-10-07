using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Notifications;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _svc;
    private readonly ICurrentUserService _me;
    public NotificationsController(INotificationService svc, ICurrentUserService me) { _svc = svc; _me = me; }
    private Guid Uid => _me.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> Get([FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(await _svc.GetAsync(Uid, q, ct)));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> Unread(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok((await _svc.GetUnreadCountAsync(Uid, ct)).Data));

    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(Guid id, CancellationToken ct)
    {
        var r = await _svc.MarkAsReadAsync(Uid, id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var r = await _svc.DeleteAsync(Uid, id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost("broadcast")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Broadcast(BroadcastNotificationRequest req, CancellationToken ct)
    {
        var r = await _svc.BroadcastAsync(req, ct);
        return Ok(ApiResponse<object>.Ok(new { }, r.Message));
    }
}