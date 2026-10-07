using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Categories;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<CategoryService> _log;

    public CategoryService(AppDbContext db, IMapper mapper, ILogger<CategoryService> log)
    { _db = db; _mapper = mapper; _log = log; }

    public async Task<PagedResult<CategoryDto>> GetPagedAsync(PaginationQuery q, CancellationToken ct = default)
    {
        var query = _db.Categories.AsNoTracking().Include(c => c.Books).OrderBy(c => c.Name);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<CategoryDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<CategoryDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result<CategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Categories.AsNoTracking().Include(x => x.Books)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? Result<CategoryDto>.Fail("Category not found") : Result<CategoryDto>.Ok(_mapper.Map<CategoryDto>(c));
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest req, CancellationToken ct = default)
    {
        if (await _db.Categories.CountAsync(ct) >= StorageConstants.MaxCategoryCount)
            return Result<CategoryDto>.Fail($"Maximum number of categories ({StorageConstants.MaxCategoryCount}) reached.");
        if (await _db.Categories.AnyAsync(c => c.Name == req.Name, ct))
            return Result<CategoryDto>.Fail("Category name already exists.");

        var c = new Category { Name = req.Name, Description = req.Description };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Category created: {Name}", c.Name);
        return Result<CategoryDto>.Ok(_mapper.Map<CategoryDto>(c), "Category created");
    }

    public async Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest req, CancellationToken ct = default)
    {
        var c = await _db.Categories.Include(x => x.Books).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CategoryDto>.Fail("Category not found");
        if (await _db.Categories.AnyAsync(x => x.Id != id && x.Name == req.Name, ct))
            return Result<CategoryDto>.Fail("Category name already exists.");
        c.Name = req.Name; c.Description = req.Description; c.IsActive = req.IsActive; c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<CategoryDto>.Ok(_mapper.Map<CategoryDto>(c), "Category updated");
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Categories.Include(x => x.Books).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail("Category not found");
        if (c.Books.Any()) return Result.Fail("Cannot delete category with books. Move or delete books first.");
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Category deleted");
    }
}