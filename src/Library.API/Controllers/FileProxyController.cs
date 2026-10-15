using Library.Domain.Constants;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.API.Controllers;

[ApiController]
[Route("api/proxy")]
[Authorize]
public class FileProxyController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<FileProxyController> _log;

    public FileProxyController(
        AppDbContext db,
        IHttpClientFactory httpFactory,
        ILogger<FileProxyController> log)
    {
        _db = db;
        _http = httpFactory.CreateClient("GutenbergProxy");
        _log = log;
    }

    /// <summary>
    /// يمرر تنزيل الكتاب عبر السيرفر (للمستخدمين الذين لا يستطيعون الوصول لـ Gutenberg)
    /// GET /api/proxy/books/{id}/download
    /// </summary>
    [HttpGet("books/{id:guid}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadBook(Guid id, CancellationToken ct)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book == null)
            return NotFound(new { success = false, message = "الكتاب غير موجود" });

        if (string.IsNullOrEmpty(book.DownloadUrl))
            return BadRequest(new { success = false, message = "لا يوجد ملف قابل للتنزيل" });

        try
        {
            _log.LogInformation("Proxying download: {Url}", book.DownloadUrl);

            // تحميل من Gutenberg
            var response = await _http.GetAsync(book.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Gutenberg returned {Status} for {Url}", response.StatusCode, book.DownloadUrl);
                return StatusCode((int)response.StatusCode, new { success = false, message = "فشل التنزيل من المصدر" });
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = $"{book.Title.Replace(" ", "_")}.epub";

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Proxy download failed for book {Id}", id);
            return StatusCode(500, new { success = false, message = "خطأ في التنزيل" });
        }
    }
}