using Library.Application.Common;
using Library.Application.DTOs.Books;
using Library.Application.Interfaces;
using Library.Infrastructure.ExternalApis;
using Library.Infrastructure.ExternalApis.DTOs;
using Library.Infrastructure.Persistence;
using Library.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.API.Controllers;

[ApiController]
[Route("api/metadata")]
[Authorize(Roles = RoleNames.Admin)]
public class MetadataController : ControllerBase
{
    private readonly IBookMetadataService _metadata;
    private readonly AppDbContext _db;
    private readonly ILogger<MetadataController> _log;

    public MetadataController(
        IBookMetadataService metadata,
        AppDbContext db,
        ILogger<MetadataController> log)
    {
        _metadata = metadata;
        _db = db;
        _log = log;
    }

    /// <summary>
    /// البحث في Open Library
    /// GET /api/metadata/search?q=programming&limit=10
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponse<object>.Fail("Query is required"));

        var results = await _metadata.SearchBooksAsync(q, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results,
            $"Found {results.Count} books"));
    }

    /// <summary>
    /// جلب بيانات كتاب بـ ISBN
    /// GET /api/metadata/isbn/9780140328721
    /// </summary>
    [HttpGet("isbn/{isbn}")]
    public async Task<IActionResult> GetByIsbn(string isbn, CancellationToken ct)
    {
        var result = await _metadata.GetByIsbnAsync(isbn, ct);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail("Book not found"));

        return Ok(ApiResponse<BookMetadataDto>.Ok(result));
    }

    /// <summary>
    /// جلب كتب تصنيف معيّن من Open Library
    /// GET /api/metadata/subject/programming?limit=20
    /// </summary>
    [HttpGet("subject/{subject}")]
    public async Task<IActionResult> GetBySubject(
        string subject,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var results = await _metadata.GetBooksBySubjectAsync(subject, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results,
            $"Found {results.Count} books for '{subject}'"));
    }

    /// <summary>
    /// استيراد دفعة كتب تصنيف معيّن إلى قاعدة البيانات
    /// POST /api/metadata/import-category
    /// Body: { "subject": "programming", "categoryId": "guid", "limit": 10 }
    /// </summary>
    [HttpPost("import-category")]
    public async Task<IActionResult> ImportCategory(
        [FromBody] ImportCategoryRequest req,
        CancellationToken ct)
    {
        // التحقق من التصنيف
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);

        if (category == null)
            return BadRequest(ApiResponse<object>.Fail("التصنيف غير موجود"));

        // جلب الكتب من Open Library
        var booksMetadata = await _metadata.GetBooksBySubjectAsync(
            req.Subject, req.Limit, ct);

        if (!booksMetadata.Any())
            return Ok(ApiResponse<object>.Ok(new { imported = 0 },
                "لا كتب متاحة لهذا الموضوع"));

        var imported = 0;
        var skipped = 0;

        foreach (var meta in booksMetadata)
        {
            try
            {
                // تحقق من عدم التكرار (بالعنوان + المؤلف)
                var exists = await _db.Books.AnyAsync(b =>
                    b.Title == meta.Title && b.Author == meta.Author, ct);

                if (exists) { skipped++; continue; }

                // تحقق من ISBN مكرر
                if (!string.IsNullOrEmpty(meta.ISBN))
                {
                    var isbnExists = await _db.Books
                        .AnyAsync(b => b.ISBN == meta.ISBN, ct);
                    if (isbnExists) { skipped++; continue; }
                }

                // أضف الكتاب
                var book = new Domain.Entities.Book
                {
                    Title = meta.Title,
                    Author = meta.Author,
                    Description = meta.Description ?? $"كتاب من تصنيف {category.Name}",
                    Publisher = meta.Publisher,
                    PublicationDate = meta.PublishYear.HasValue
                        ? new DateTime(meta.PublishYear.Value, 1, 1)
                        : null,
                    ISBN = meta.ISBN,
                    Language = meta.Language ?? "en",
                    CoverImage = meta.CoverUrl,
                    CategoryId = category.Id,
                    IsPublished = true,
                    Format = Domain.Enums.BookFormat.Pdf,
                    FileSize = 0
                };

                _db.Books.Add(book);
                imported++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import book: {Title}", meta.Title);
                skipped++;
            }
        }

        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Imported {Imported} books to {Category}, skipped {Skipped}",
            imported, category.Name, skipped);

        return Ok(ApiResponse<object>.Ok(new
        {
            imported,
            skipped,
            category = category.Name,
            subject = req.Subject
        }, $"تم استيراد {imported} كتاب إلى تصنيف {category.Name}"));
    }

    /// <summary>
    /// استيراد كتاب واحد بالـ ISBN
    /// POST /api/metadata/import-isbn
    /// Body: { "isbn": "9780140328721", "categoryId": "guid" }
    /// </summary>
    [HttpPost("import-isbn")]
    public async Task<IActionResult> ImportByIsbn(
        [FromBody] ImportByIsbnRequest req,
        CancellationToken ct)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(ApiResponse<object>.Fail("التصنيف غير موجود"));

        var meta = await _metadata.GetByIsbnAsync(req.Isbn, ct);
        if (meta == null)
            return NotFound(ApiResponse<object>.Fail("لم يُعثر على الكتاب"));

        var exists = await _db.Books.AnyAsync(b => b.ISBN == meta.ISBN, ct);
        if (exists)
            return BadRequest(ApiResponse<object>.Fail("الكتاب موجود مسبقاً"));

        var book = new Domain.Entities.Book
        {
            Title = meta.Title,
            Author = meta.Author,
            Description = meta.Description,
            Publisher = meta.Publisher,
            PublicationDate = meta.PublishYear.HasValue
                ? new DateTime(meta.PublishYear.Value, 1, 1)
                : null,
            ISBN = meta.ISBN,
            Language = meta.Language ?? "en",
            CoverImage = meta.CoverUrl,
            CategoryId = category.Id,
            IsPublished = true,
            Format = Domain.Enums.BookFormat.Pdf,
            FileSize = 0
        };

        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { bookId = book.Id },
            "تم استيراد الكتاب بنجاح"));
    }
}

public record ImportCategoryRequest(string Subject, Guid CategoryId, int Limit = 10);
public record ImportByIsbnRequest(string Isbn, Guid CategoryId);