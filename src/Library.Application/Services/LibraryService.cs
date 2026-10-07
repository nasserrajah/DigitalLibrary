using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.DTOs.Library;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Library.Domain.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Application.Services;

public class LibraryService : ILibraryService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _storage;
    private readonly ILogger<LibraryService> _log;

    public LibraryService(AppDbContext db, IMapper mapper, IFileStorageService storage, ILogger<LibraryService> log)
    { _db = db; _mapper = mapper; _storage = storage; _log = log; }

    public async Task<PagedResult<LibraryItemDto>> GetAsync(Guid userId, PaginationQuery q, CancellationToken ct = default)
    {
        var query = _db.UserLibrary.AsNoTracking()
            .Include(x => x.Book).Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<LibraryItemDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<LibraryItemDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result<LibraryItemDto>> AddAsync(Guid userId, AddToLibraryRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == req.BookId, ct);
        if (book is null) return Result<LibraryItemDto>.Fail("Book not found");

        if (await _db.UserLibrary.AnyAsync(x => x.UserId == userId && x.BookId == book.Id, ct))
            return Result<LibraryItemDto>.Fail("Book already in your library.");

        var size = book.FileSize;
        if (!user.HasStorageFor(size))
            return Result<LibraryItemDto>.Fail(
                $"Storage limit exceeded. Used {user.StorageUsed / 1024 / 1024} MB of {user.StorageLimit / 1024 / 1024} MB. " +
                $"File size: {size / 1024 / 1024} MB.");

        var item = new UserLibrary
        {
            UserId = userId, BookId = book.Id, FileSize = size, IsImported = false
        };
        _db.UserLibrary.Add(item);
        user.StorageUsed += size;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("User {UserId} added book {BookId} ({Size}B)", userId, book.Id, size);
        return Result<LibraryItemDto>.Ok(_mapper.Map<LibraryItemDto>(item), "Book added to library");
    }

    public async Task<Result<LibraryItemDto>> ImportAsync(Guid userId, Stream stream, string fileName, string title, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!StorageConstants.AllowedBookExtensions.Contains(ext))
            return Result<LibraryItemDto>.Fail("Unsupported file format.");

        var tmp = Path.GetTempFileName();
        long size;
        try
        {
            await using (var fs = File.Create(tmp)) { await stream.CopyToAsync(fs, ct); size = fs.Length; }
            if (size > StorageConstants.MaxBookFileBytes)
                return Result<LibraryItemDto>.Fail("File too large.");
            if (!user.HasStorageFor(size))
                return Result<LibraryItemDto>.Fail(
                    $"Storage limit exceeded. Used {user.StorageUsed / 1024 / 1024} MB of {user.StorageLimit / 1024 / 1024} MB.");

            // We create a placeholder public "book" so user can keep it personal
            var formatEnum = ext switch
            {
                ".pdf" => BookFormat.Pdf, ".epub" => BookFormat.Epub,
                ".mobi" => BookFormat.Mobi, ".txt" => BookFormat.Txt, _ => BookFormat.Other
            };

            var defaultCat = await _db.Categories.FirstAsync(ct);

            var book = new Book
            {
                Title = title, Author = "Unknown", Format = formatEnum,
                CategoryId = defaultCat.Id, IsPublished = false,
                FileSize = size, FileName = fileName
            };

            await using var read = File.OpenRead(tmp);
            var saved = await _storage.SaveAsync(read, fileName, "user-imports", ct);
            book.FilePath = saved.StorageKey;
            _db.Books.Add(book);
            await _db.SaveChangesAsync(ct);

            var item = new UserLibrary
            {
                UserId = userId, BookId = book.Id, FileSize = size,
                IsImported = true, LocalStorageKey = saved.StorageKey,
                OriginalFileName = fileName
            };
            _db.UserLibrary.Add(item);
            user.StorageUsed += size;
            await _db.SaveChangesAsync(ct);
            return Result<LibraryItemDto>.Ok(_mapper.Map<LibraryItemDto>(item), "Book imported");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid libraryItemId, CancellationToken ct = default)
    {
        var item = await _db.UserLibrary.FirstOrDefaultAsync(x => x.Id == libraryItemId && x.UserId == userId, ct);
        if (item is null) return Result.Fail("Item not found");

        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        user.StorageUsed = Math.Max(0, user.StorageUsed - item.FileSize);

        if (item.IsImported && !string.IsNullOrEmpty(item.LocalStorageKey))
            await _storage.DeleteAsync(item.LocalStorageKey, ct);

        _db.UserLibrary.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Book removed from library");
    }

    public async Task<Result> UpdateStatusAsync(Guid userId, Guid libraryItemId, LibraryBookStatus status, CancellationToken ct = default)
    {
        var item = await _db.UserLibrary.FirstOrDefaultAsync(x => x.Id == libraryItemId && x.UserId == userId, ct);
        if (item is null) return Result.Fail("Item not found");
        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Status updated");
    }
}