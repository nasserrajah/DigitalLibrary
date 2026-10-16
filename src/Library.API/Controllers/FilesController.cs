using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;

    public FilesController(IFileStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// يقدّم أي ملف من التخزين (صور البروفايل، أغلفة، ملفات كتب)
    /// GET /api/files/{**path}
    /// مثال: /api/files/profiles/2026/10/abc.jpg
    /// </summary>
    [HttpGet("{**storageKey}")]
    [AllowAnonymous]  // عام — لأن صور البروفايل يجب أن تظهر
    public async Task<IActionResult> GetFile(
        string storageKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return NotFound();

        // منع Path Traversal
        var cleanKey = storageKey.Replace("..", "").TrimStart('/');

        var stream = await _storage.OpenReadAsync(cleanKey, ct);
        if (stream is null)
            return NotFound(new { message = "File not found" });

        // اكتشف Content-Type من الامتداد
        var ext = Path.GetExtension(cleanKey).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".epub" => "application/epub+zip",
            ".mobi" => "application/x-mobipocket-ebook",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

        // Cache: صور البروفايل تُحدث نادراً
        Response.Headers["Cache-Control"] = "public, max-age=3600";

        return File(stream, contentType, enableRangeProcessing: true);
    }
}