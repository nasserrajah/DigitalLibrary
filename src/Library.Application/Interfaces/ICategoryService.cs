// ICategoryService.cs
using Library.Application.Common;
using Library.Application.DTOs.Auth;

namespace Library.Application.Interfaces;
public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetPagedAsync(PaginationQuery q, CancellationToken ct = default);
    Task<Result<CategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest req, CancellationToken ct = default);
    Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest req, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}