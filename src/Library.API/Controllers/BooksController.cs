using Library.Application.Common;
using Library.Application.DTOs.Books;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController : ControllerBase
{
    private readonly IBookService _svc;
    public BooksController(IBookService svc) => _svc = svc;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<BookSummaryDto>>>> GetAll(
        [FromQuery] PaginationQuery q, [FromQuery] Guid? categoryId, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<BookSummaryDto>>.Ok(await _svc.GetPagedAsync(q, categoryId, ct)));

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<BookSummaryDto>>>> Search(
        [FromQuery] string query, [FromQuery] PaginationQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(ApiResponse<object>.Fail("Query is required."));
        return Ok(ApiResponse<PagedResult<BookSummaryDto>>.Ok(await _svc.SearchAsync(query, q, ct)));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookDto>>> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetByIdAsync(id, ct);
        return r.Success ? Ok(ApiResponse<BookDto>.Ok(r.Data!, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<BookDto>>> Create(CreateBookRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(req, ct);
        return r.Success ? Ok(ApiResponse<BookDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<BookDto>>> Update(Guid id, UpdateBookRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r.Success ? Ok(ApiResponse<BookDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var r = await _svc.DeleteAsync(id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost("{id:guid}/file")]
    [Authorize(Roles = RoleNames.Admin)]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BookDto>>> UploadFile(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        var r = await _svc.UploadFileAsync(id, s, file.FileName, ct);
        return r.Success ? Ok(ApiResponse<BookDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost("{id:guid}/cover")]
    [Authorize(Roles = RoleNames.Admin)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BookDto>>> UploadCover(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        var r = await _svc.UploadCoverAsync(id, s, file.FileName, ct);
        return r.Success ? Ok(ApiResponse<BookDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message));
    }

    [HttpGet("{id:guid}/download")]
    [Authorize]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var r = await _svc.DownloadAsync(id, ct);
        if (!r.Success) return NotFound(ApiResponse<object>.Fail(r.Message));
        var (stream, name, type) = r.Data!;
        return File(stream, type, name);
    }
}