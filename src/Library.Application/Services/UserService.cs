using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    public UserService(AppDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? Result<UserDto>.Fail("Not found") : Result<UserDto>.Ok(_mapper.Map<UserDto>(u));
    }

    public async Task<PagedResult<UserDto>> GetPagedAsync(PaginationQuery q, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<UserDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result> ToggleActiveAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Result.Fail("Not found");
        u.IsActive = !u.IsActive; u.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok(u.IsActive ? "User activated" : "User deactivated");
    }
}