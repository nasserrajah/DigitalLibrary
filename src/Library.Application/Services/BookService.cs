using AutoMapper;
using AutoMapper.QueryableExtensions;
using Library.Application.Common;
using Library.Application.DTOs.Books;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Application.Services;

public class BookService : IBookService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _storage;
    private readonly ILogger<BookService> _log;

    public BookService(AppDbContext db, IMapper mapper, IFileStorageService storage, ILogger<BookService> log)
    { _db = db; _mapper = mapper; _storage = storage; _log = log; }

    public async Task<PagedResult<BookSummaryDto>> GetPagedAsync(PaginationQuery q, Guid? categoryId, CancellationToken ct = default)
    {
        var query = _db.Books.AsNoTracking().Where(b => b.IsPublished);
        if (categoryId is not null) query = query.Where(b => b.CategoryId == categoryId);
        query = query.OrderByDescending(b => b.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<BookSummaryDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<BookSummaryDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<PagedResult<BookSummaryDto>> SearchAsync(string query, PaginationQuery q, CancellationToken ct = default)
    {
        var term = query.Trim().ToLower();
        var qry = _db.Books.AsNoTracking().Where(b => b.IsPublished &&
            (b.Title.ToLower().Contains(term) ||
             b.Author.ToLower().Contains(term) ||
             (b.ISBN != null && b.ISBN.Contains(term)) ||
             (b.Description != null && b.Description.ToLower().Contains(term)) ||
             b.Category!.Name.ToLower().Contains(term)));
        qry = qry.OrderByDescending(b => b.CreatedAt);
        var total = await qry.CountAsync(ct);
        var items = await qry.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ProjectTo<BookSummaryDto>(_mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResult<BookSummaryDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<Result<BookDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Books.AsNoTracking().Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return b is null ? Result<BookDto>.Fail("Book not found") : Result<BookDto>.Ok(_mapper.Map<BookDto>(b));
    }

    public async Task<Result<BookDto>> CreateAsync(CreateBookRequest req, CancellationToken ct = default)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == req.CategoryId && c.IsActive, ct))
            return Result<BookDto>.Fail("Invalid category.");
        if (!string.IsNullOrWhiteSpace(req.ISBN) && await _db.Books.AnyAsync(b => b.ISBN == req.ISBN, ct))
            return Result<BookDto>.Fail("ISBN already exists.");

        var book = new Book
        {
            Title = req.Title, Description = req.Description, Author = req.Author,
            Publisher = req.Publisher, PublicationDate = req.PublicationDate, ISBN = req.ISBN,
            Language = req.Language, Format = req.Format, CategoryId = req.CategoryId,
            IsPublished = req.IsPublished
        };
        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);
        return Result<BookDto>.Ok(_mapper.Map<BookDto>(book), "Book created");
    }

    public async Task<Result<BookDto>> UpdateAsync(Guid id, UpdateBookRequest req, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Result<BookDto>.Fail("Book not found");
        if (!await _db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct))
            return Result<BookDto>.Fail("Invalid category.");

        book.Title = req.Title; book.Description = req.Description; book.Author = req.Author;
        book.Publisher = req.Publisher; book.PublicationDate = req.PublicationDate;
        book.ISBN = req.ISBN; book.Language = req.Language; book.Format = req.Format;
        book.CategoryId = req.CategoryId; book.IsPublished = req.IsPublished;
        book.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<BookDto>.Ok(_mapper.Map<BookDto>(book), "Book updated");
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Result.Fail("Book not found");
        if (!string.IsNullOrEmpty(book.FilePath)) await _storage.DeleteAsync(book.FilePath, ct);
        if (!string.IsNullOrEmpty(book.CoverImage)) await _storage.DeleteAsync(book.CoverImage, ct);
        _db.Books.Remove(book);
        await _db.SaveChangesAsync(ct);
        return Result.Ok("Book deleted");
    }

    public async Task<Result<BookDto>> UploadFileAsync(Guid id, Stream stream, string fileName, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Result<BookDto>.Fail("Book not found");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!StorageConstants.AllowedBookExtensions.Contains(ext))
            return Result<BookDto>.Fail("Unsupported file format.");

        // size cap: buffer to temp to validate size
        var tmp = Path.GetTempFileName();
        long size;
        try
        {
            await using (var fs = File.Create(tmp)) { await stream.CopyToAsync(fs, ct); size = fs.Length; }
            if (size > StorageConstants.MaxBookFileBytes)
                return Result<BookDto>.Fail("File too large.");

            if (!string.IsNullOrEmpty(book.FilePath)) await _storage.DeleteAsync(book.FilePath, ct);
            await using var read = File.OpenRead(tmp);
            var saved = await _storage.SaveAsync(read, fileName, "books", ct);
            book.FileName = saved.OriginalName;
            book.FilePath = saved.StorageKey;
            book.FileSize = saved.Size;
            await _db.SaveChangesAsync(ct);
            return Result<BookDto>.Ok(_mapper.Map<BookDto>(book), "File uploaded");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public async Task<Result<BookDto>> UploadCoverAsync(Guid id, Stream stream, string fileName, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Result<BookDto>.Fail("Book not found");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!StorageConstants.AllowedImageExtensions.Contains(ext))
            return Result<BookDto>.Fail("Unsupported image format.");

        if (!string.IsNullOrEmpty(book.CoverImage)) await _storage.DeleteAsync(book.CoverImage, ct);
        var saved = await _storage.SaveAsync(stream, fileName, "covers", ct);
        book.CoverImage = saved.StorageKey;
        await _db.SaveChangesAsync(ct);
        return Result<BookDto>.Ok(_mapper.Map<BookDto>(book), "Cover uploaded");
    }

    public async Task<Result<(Stream, string, string)>> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null || string.IsNullOrEmpty(book.FilePath))
            return Result<(Stream, string, string)>.Fail("File not available.");
        var stream = await _storage.OpenReadAsync(book.FilePath, ct);
        if (stream is null) return Result<(Stream, string, string)>.Fail("File missing from storage.");
        return Result<(Stream, string, string)>.Ok((stream, book.FileName ?? $"{book.Title}.{book.Format}", "application/octet-stream"));
    }
}