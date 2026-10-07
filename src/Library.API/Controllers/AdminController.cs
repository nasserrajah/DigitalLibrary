using Library.Application.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminStatsService _stats;
    public AdminController(IAdminStatsService stats) => _stats = stats;

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<object>>> Stats(CancellationToken ct)
    {
        var r = await _stats.GetStatsAsync(ct);
        return Ok(ApiResponse<object>.Ok(r.Data!, r.Message));
    }
}