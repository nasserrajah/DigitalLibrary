using Library.Application.Common;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Library.Infrastructure.Persistence; // تأكد من مسار AppDbContext

namespace Library.API.Controllers;

[ApiController]
[Route("api/admin/cleanup")]
[Authorize] // محمي بالتوكن
public class AdminCleanupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _me;

    public AdminCleanupController(AppDbContext db, ICurrentUserService me)
    {
        _db = db;
        _me = me;
    }

    [HttpDelete("remove-openlibrary-books")]
public async Task<ActionResult<ApiResponse<object>>> RemoveOpenLibraryBooks(
    CancellationToken ct)
{
    if (!_me.IsAdmin)
        return Forbid();

    // 🆕 اكتشف كتب Open Library بأكثر من طريقة
    var openLibraryBooks = await _db.Books
        .Where(b =>
            // 1) Gutenberg (الأحدث)
            b.GutenbergId != null
            // 2) روابط Gutenberg
            || (b.DownloadUrl != null && b.DownloadUrl.Contains("gutenberg"))
            // 3) غلاف من Open Library
            || (b.CoverImage != null && b.CoverImage.Contains("openlibrary.org"))
            // 4) غلاف من Gutenberg
            || (b.CoverImage != null && b.CoverImage.Contains("gutenberg.org"))
            // 5) الناشر Project Gutenberg
            || (b.Publisher != null && b.Publisher.Contains("Gutenberg"))
            // 6) كتب بلا ملف + حجم 0 (من imports القديمة)
            || (b.FileSize == 0
                && string.IsNullOrEmpty(b.FileName)
                && string.IsNullOrEmpty(b.FilePath)))
        .ToListAsync(ct);

    if (!openLibraryBooks.Any())
        return Ok(ApiResponse<object>.Ok(
            new { deleted = 0 },
            "No Open Library books found."));

    _db.Books.RemoveRange(openLibraryBooks);
    await _db.SaveChangesAsync(ct);

    return Ok(ApiResponse<object>.Ok(
        new { deleted = openLibraryBooks.Count },
        $"Successfully deleted {openLibraryBooks.Count} Open Library books."));
}

}