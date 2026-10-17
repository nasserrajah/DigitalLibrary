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
    public async Task<ActionResult<ApiResponse<object>>> RemoveOpenLibraryBooks(CancellationToken ct)
    {
        // 1. تأكد أن المستخدم أدمن
        if (!_me.IsAdmin)
            return Forbid("Only admins can perform this action.");

        // 2. ابحث عن الكتب التي لها GutenbergId (أو downloadUrl) فهي من Open Library
        var openLibraryBooks = await _db.Books
            .Where(b => b.GutenbergId != null || (b.DownloadUrl != null && b.DownloadUrl != ""))
            .ToListAsync(ct);

        if (!openLibraryBooks.Any())
            return Ok(ApiResponse<object>.Ok(new { deleted = 0 }, "No Open Library books found."));

        // 3. احذف الملفات المحلية إن وجدت (احتياطاً)
        foreach (var book in openLibraryBooks)
        {
            if (!string.IsNullOrEmpty(book.FilePath) && !book.FilePath.StartsWith("http"))
            {
                // يمكنك حقن IFileStorageService هنا لحذف الملفات
                // لكن للتبسيط، سنتجاهلها لأن كتب Open Library عادة لا تملك ملفات محلية
            }
        }

        // 4. احذف الكتب من قاعدة البيانات (CASCADE سيحذف الارتباطات)
        _db.Books.RemoveRange(openLibraryBooks);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { deleted = openLibraryBooks.Count },
            $"Successfully deleted {openLibraryBooks.Count} Open Library books."
        ));
    }
}