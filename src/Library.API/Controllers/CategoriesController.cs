using Library.Application.Common;
using Library.Application.DTOs.Categories;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _svc;
    public CategoriesController(ICategoryService svc) => _svc = svc;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<CategoryDto>>>> GetAll([FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<CategoryDto>>.Ok(await _svc.GetPagedAsync(q, ct)));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Get(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetByIdAsync(id, ct);
        return r.Success ? Ok(ApiResponse<CategoryDto>.Ok(r.Data!, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(CreateCategoryRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(req, ct);
        return r.Success ? Ok(ApiResponse<CategoryDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(Guid id, UpdateCategoryRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r.Success ? Ok(ApiResponse<CategoryDto>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var r = await _svc.DeleteAsync(id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }
}