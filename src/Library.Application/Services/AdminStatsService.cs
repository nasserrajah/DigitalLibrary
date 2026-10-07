using Library.Application.Common;
using Library.Application.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

public class AdminStatsService : IAdminStatsService
{
    private readonly AppDbContext _db;
    public AdminStatsService(AppDbContext db) => _db = db;

    public async Task<Result<object>> GetStatsAsync(CancellationToken ct = default)
    {
        var users = await _db.Users.CountAsync(ct);
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive, ct);
        var books = await _db.Books.CountAsync(ct);
        var published = await _db.Books.CountAsync(b => b.IsPublished, ct);
        var categories = await _db.Categories.CountAsync(ct);
        var favorites = await _db.Favorites.CountAsync(ct);
        var libraryItems = await _db.UserLibrary.CountAsync(ct);
        var totalStorage = await _db.Users.SumAsync(u => (long?)u.StorageUsed, ct) ?? 0;

        return Result<object>.Ok(new
        {
            users, activeUsers, books, publishedBooks = published,
            categories, favorites, libraryItems,
            totalStorageBytes = totalStorage,
            totalStorageMB = totalStorage / 1024 / 1024
        });
    }
}