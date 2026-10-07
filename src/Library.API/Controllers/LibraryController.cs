using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Library;
using Library.Application.Interfaces;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly ILibraryService _svc;
    private readonly ICurrentUserService _me;
    public LibraryController(ILibraryService svc, ICurrentUserService me) { _svc = svc; _me = me; }

    private Guid Uid => _me.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LibraryItemDto>>>> Get([FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<LibraryItemDto>>.Ok(await _svc.GetAsync(Uid, q, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<LibraryItemDto>>> Add(AddToLibraryRequest req, CancellationToken ct)
    {
        var r = await _svc.AddAsync(Uid, req, ct);
        return r.Success ? Ok(ApiResponse<LibraryItemDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPost("import")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<LibraryItemDto>>> Import(
        IFormFile file, [FromForm] string title, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        var r = await _svc.ImportAsync(Uid, s, file.FileName, title, ct);
        return r.Success ? Ok(ApiResponse<LibraryItemDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Remove(Guid id, CancellationToken ct)
    {
        var r = await _svc.RemoveAsync(Uid, id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(Guid id, UpdateLibraryStatusRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateStatusAsync(Uid, id, req.Status, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }
}