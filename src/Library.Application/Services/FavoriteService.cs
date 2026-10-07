using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Favorites;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class FavoriteService : IFavoriteService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    public FavoriteService(AppDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<PagedResult<FavoriteDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default)
    {
        var query = _db.Favorites.AsNoTracking().Include(f => f.Book)
            .Where(f => f.UserId == userId).OrderByDescending(f => f.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<FavoriteDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<FavoriteDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result<FavoriteDto>> AddAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        if (!await _db.Books.AnyAsync(b => b.Id == bookId, ct))
            return Result<FavoriteDto>.Fail("Book not found");
        if (await _db.Favorites.AnyAsync(f => f.UserId == userId && f.BookId == bookId, ct))
            return Result<FavoriteDto>.Fail("Already in favorites.");

        var fav = new Favorite { UserId = userId, BookId = bookId };
        _db.Favorites.Add(fav);
        await _db.SaveChangesAsync(ct);

        var withBook = await _db.Favorites.AsNoTracking().Include(f => f.Book)
            .FirstAsync(f => f.Id == fav.Id, ct);
        return Result<FavoriteDto>.Ok(_mapper.Map<FavoriteDto>(withBook), "Added to favorites");
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.BookId == bookId, ct);
        if (fav is null) return Result.Fail("Not found");
        _db.Favorites.Remove(fav);
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Removed from favorites");
    }

    public async Task<Result<bool>> ExistsAsync(Guid userId, Guid bookId, CancellationToken ct = default)
        => Result<bool>.Ok(await _db.Favorites.AnyAsync(f => f.UserId == userId && f.BookId == bookId, ct));
}