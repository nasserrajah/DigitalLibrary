using Library.Application.Common;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.API.Controllers;

[ApiController]
[Route("api/admin/cleanup")]
[Authorize]
public class AdminCleanupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _me;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _cfg;

    public AdminCleanupController(
        AppDbContext db,
        ICurrentUserService me,
        UserManager<ApplicationUser> users,
        IConfiguration cfg)
    {
        _db = db;
        _me = me;
        _users = users;
        _cfg = cfg;
    }

    // 🆕 ترقية المستخدم الحالي إلى Admin باستخدام مفتاح سري
    [HttpPost("promote-me")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> PromoteMe(
        [FromHeader(Name = "X-Setup-Key")] string? setupKey,
        CancellationToken ct)
    {
        var expectedKey = _cfg["Setup:SecretKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return StatusCode(503, ApiResponse<object>.Fail(
                "Setup key not configured on server."));

        if (setupKey != expectedKey)
            return Unauthorized(ApiResponse<object>.Fail("Invalid setup key."));

        var userId = _me.UserId;
        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail(
                "You must be logged in to promote yourself."));

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        if (await _users.IsInRoleAsync(user, RoleNames.Admin))
            return Ok(ApiResponse<object>.Ok(
                new { userId = user.Id, role = "Admin", alreadyAdmin = true },
                "You are already an Admin."));

        await _users.AddToRoleAsync(user, RoleNames.Admin);

        return Ok(ApiResponse<object>.Ok(
            new { userId = user.Id, email = user.Email, role = "Admin" },
            "Promoted to Admin successfully. Please logout and login again."));
    }

    [HttpDelete("remove-openlibrary-books")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveOpenLibraryBooks(
        CancellationToken ct)
    {
        if (!_me.IsAdmin)
            return Forbid();

        var openLibraryBooks = await _db.Books
            .Where(b => b.GutenbergId != null ||
                       (b.DownloadUrl != null && b.DownloadUrl != ""))
            .ToListAsync(ct);

        if (!openLibraryBooks.Any())
            return Ok(ApiResponse<object>.Ok(
                new { deleted = 0 }, "No Open Library books found."));

        _db.Books.RemoveRange(openLibraryBooks);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { deleted = openLibraryBooks.Count },
            $"Successfully deleted {openLibraryBooks.Count} Open Library books."));
    }

    [HttpGet("whoami")]
    public async Task<ActionResult<ApiResponse<object>>> WhoAmI(CancellationToken ct)
    {
        var userId = _me.UserId;
        if (userId is null) return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user is null) return NotFound();

        var roles = await _users.GetRolesAsync(user);
        return Ok(ApiResponse<object>.Ok(new
        {
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            roles,
            isAdmin = roles.Contains(RoleNames.Admin)
        }));
    }
}