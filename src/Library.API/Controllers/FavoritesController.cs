using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Favorites;
using Library.Application.Interfaces;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _svc;
    private readonly ICurrentUserService _me;
    public FavoritesController(IFavoriteService svc, ICurrentUserService me) { _svc = svc; _me = me; }
    private Guid Uid => _me.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FavoriteDto>>>> Get([FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<FavoriteDto>>.Ok(await _svc.GetAsync(Uid, q, ct)));

    [HttpPost("{bookId:guid}")]
    public async Task<ActionResult<ApiResponse<FavoriteDto>>> Add(Guid bookId, CancellationToken ct)
    {
        var r = await _svc.AddAsync(Uid, bookId, ct);
        return r.Success ? Ok(ApiResponse<FavoriteDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }

    [HttpDelete("{bookId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Remove(Guid bookId, CancellationToken ct)
    {
        var r = await _svc.RemoveAsync(Uid, bookId, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpGet("{bookId:guid}/exists")]
    public async Task<ActionResult<ApiResponse<bool>>> Exists(Guid bookId, CancellationToken ct)
        => Ok(ApiResponse<bool>.Ok((await _svc.ExistsAsync(Uid, bookId, ct)).Data));
}