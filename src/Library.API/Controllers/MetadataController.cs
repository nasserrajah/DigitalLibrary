using Library.Application.Common;
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
    private readonly IGutendexService _gutenberg;
    private readonly AppDbContext _db;
    private readonly ILogger<MetadataController> _log;

    public MetadataController(
        IBookMetadataService metadata,
        IGutendexService gutenberg,
        AppDbContext db,
        ILogger<MetadataController> log)
    {
        _metadata = metadata;
        _gutenberg = gutenberg;
        _db = db;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════
    // Open Library Endpoints (كما هو موجود)
    // ═══════════════════════════════════════════════════════

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponse<object>.Fail("Query is required"));

        var results = await _metadata.SearchBooksAsync(q, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results, $"Found {results.Count} books"));
    }

    [HttpGet("isbn/{isbn}")]
    public async Task<IActionResult> GetByIsbn(string isbn, CancellationToken ct)
    {
        var result = await _metadata.GetByIsbnAsync(isbn, ct);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Book not found"));
        return Ok(ApiResponse<BookMetadataDto>.Ok(result));
    }

    [HttpGet("subject/{subject}")]
    public async Task<IActionResult> GetBySubject(
        string subject, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var results = await _metadata.GetBooksBySubjectAsync(subject, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results, $"Found {results.Count}"));
    }

    [HttpPost("import-category")]
    public async Task<IActionResult> ImportCategory(
        [FromBody] ImportCategoryRequest req, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(ApiResponse<object>.Fail("التصنيف غير موجود"));

        var booksMetadata = await _metadata.GetBooksBySubjectAsync(req.Subject, req.Limit, ct);
        if (!booksMetadata.Any())
            return Ok(ApiResponse<object>.Ok(new { imported = 0, skipped = 0 }, "لا كتب"));

        return await ImportBatchAsync(booksMetadata, category, ct);
    }

    // ═══════════════════════════════════════════════════════
    // 🆕 Project Gutenberg Endpoints
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// بحث في Project Gutenberg
    /// GET /api/metadata/gutenberg/search?q=shakespeare
    /// </summary>
    [HttpGet("gutenberg/search")]
    public async Task<IActionResult> GutenbergSearch(
        [FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponse<object>.Fail("Query is required"));

        var results = await _gutenberg.SearchAsync(q, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results,
            $"Found {results.Count} books with downloadable files"));
    }

    /// <summary>
    /// جلب كتب Gutenberg حسب الموضوع
    /// GET /api/metadata/gutenberg/topic/fiction
    /// </summary>
    [HttpGet("gutenberg/topic/{topic}")]
    public async Task<IActionResult> GutenbergByTopic(
        string topic, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var results = await _gutenberg.GetByTopicAsync(topic, limit, ct);
        return Ok(ApiResponse<List<BookMetadataDto>>.Ok(results,
            $"Found {results.Count} books"));
    }

    /// <summary>
    /// استيراد دفعة من Gutenberg إلى تصنيف معيّن
    /// POST /api/metadata/gutenberg/import-category
    /// Body: { "topic": "fiction", "categoryId": "guid", "limit": 10 }
    /// </summary>
    [HttpPost("gutenberg/import-category")]
    public async Task<IActionResult> GutenbergImportCategory(
        [FromBody] ImportCategoryRequest req, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (category == null)
            return BadRequest(ApiResponse<object>.Fail("التصنيف غير موجود"));

        var booksMetadata = await _gutenberg.GetByTopicAsync(req.Subject, req.Limit, ct);
        if (!booksMetadata.Any())
            return Ok(ApiResponse<object>.Ok(new { imported = 0, skipped = 0 },
                "لا كتب متاحة لهذا الموضوع في Gutenberg"));

        return await ImportBatchAsync(booksMetadata, category, ct);
    }

    // ═══════════════════════════════════════════════════════
    // Shared Import Logic
    // ═══════════════════════════════════════════════════════

    private async Task<IActionResult> ImportBatchAsync(
        List<BookMetadataDto> booksMetadata,
        Library.Domain.Entities.Category category,
        CancellationToken ct)
    {
        var imported = 0;
        var skipped = 0;

        foreach (var meta in booksMetadata)
        {
            try
            {
                // تحقق من عدم التكرار
                var exists = await _db.Books.AnyAsync(b =>
                    b.Title == meta.Title && b.Author == meta.Author, ct);
                if (exists) { skipped++; continue; }

                if (meta.GutenbergId.HasValue)
                {
                    var gExists = await _db.Books
                        .AnyAsync(b => b.GutenbergId == meta.GutenbergId, ct);
                    if (gExists) { skipped++; continue; }
                }

                var book = new Library.Domain.Entities.Book
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
                    Format = DetermineFormat(meta.DownloadUrl),
                    FileSize = 0,
                    // 🆕
                    GutenbergId = meta.GutenbergId,
                    DownloadUrl = meta.DownloadUrl
                };

                _db.Books.Add(book);
                imported++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import: {Title}", meta.Title);
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
            category = category.Name
        }, $"تم استيراد {imported} كتاب إلى تصنيف {category.Name}"));
    }

    private static Library.Domain.Enums.BookFormat DetermineFormat(string? url)
    {
        if (string.IsNullOrEmpty(url)) return Library.Domain.Enums.BookFormat.Pdf;
        if (url.Contains(".epub", StringComparison.OrdinalIgnoreCase))
            return Library.Domain.Enums.BookFormat.Epub;
        if (url.Contains(".pdf", StringComparison.OrdinalIgnoreCase))
            return Library.Domain.Enums.BookFormat.Pdf;
        if (url.Contains(".txt", StringComparison.OrdinalIgnoreCase))
            return Library.Domain.Enums.BookFormat.Txt;
        return Library.Domain.Enums.BookFormat.Pdf;
    }
}

public record ImportCategoryRequest(string Subject, Guid CategoryId, int Limit = 10);