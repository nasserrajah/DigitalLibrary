using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _svc;
    private readonly ICurrentUserService _me;
    public UsersController(IUserService svc, ICurrentUserService me) { _svc = svc; _me = me; }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
    {
        var r = await _svc.GetMeAsync(_me.UserId!.Value, ct);
        return r.Success ? Ok(ApiResponse<UserDto>.Ok(r.Data!, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetAll([FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<UserDto>>.Ok(await _svc.GetPagedAsync(q, ct)));

    [HttpPut("{id:guid}/toggle-active")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Toggle(Guid id, CancellationToken ct)
    {
        var r = await _svc.ToggleActiveAsync(id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }
}