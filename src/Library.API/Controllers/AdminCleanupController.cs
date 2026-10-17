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
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly IConfiguration _cfg;

    private const string DefaultSetupKey = "SmartLib-Setup-2026-X9k7P3mQ8nR5t";

    public AdminCleanupController(
        AppDbContext db,
        ICurrentUserService me,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        IConfiguration cfg)
    {
        _db = db;
        _me = me;
        _users = users;
        _roles = roles;
        _cfg = cfg;
    }

    // ═══════════════════════════════════════════════════════
    // 🆕 ترقية المستخدم الحالي إلى Admin
    // ═══════════════════════════════════════════════════════
    [HttpPost("promote-me")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> PromoteMe(
        [FromHeader(Name = "X-Setup-Key")] string? setupKey,
        CancellationToken ct)
    {
        var expectedKey = _cfg["Setup:SecretKey"] ?? DefaultSetupKey;
        if (setupKey != expectedKey)
            return Unauthorized(ApiResponse<object>.Fail("Invalid setup key."));

        var userId = _me.UserId;
        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail(
                "You must be logged in. Please login in the app first, then try again."));

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user is null)
            return NotFound(ApiResponse<object>.Fail($"User {userId} not found in database."));

        // ✅ 1) تأكد أن دور Admin موجود في قاعدة البيانات
        if (!await _roles.RoleExistsAsync(RoleNames.Admin))
        {
            var createResult = await _roles.CreateAsync(
                new IdentityRole<Guid>(RoleNames.Admin));
            if (!createResult.Succeeded)
                return BadRequest(ApiResponse<object>.Fail(
                    "Failed to create 'Admin' role: " +
                    string.Join(", ", createResult.Errors.Select(e => e.Description))));
        }

        // ✅ 2) إذا كان Admin بالفعل
        if (await _users.IsInRoleAsync(user, RoleNames.Admin))
            return Ok(ApiResponse<object>.Ok(
                new { userId = user.Id, role = "Admin", alreadyAdmin = true },
                "You are already an Admin."));

        // ✅ 3) أضف الدور **وتحقق من النتيجة**
        var addResult = await _users.AddToRoleAsync(user, RoleNames.Admin);
        if (!addResult.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(
                "Failed to add Admin role: " +
                string.Join(", ", addResult.Errors.Select(e => e.Description))));

        // ✅ 4) تحقق نهائي من قاعدة البيانات
        var userRoles = await _users.GetRolesAsync(user);
        if (!userRoles.Contains(RoleNames.Admin))
            return BadRequest(ApiResponse<object>.Fail(
                "Role was not persisted. Please check database connection."));

        return Ok(ApiResponse<object>.Ok(new
        {
            userId = user.Id,
            email = user.Email,
            roles = userRoles,
            role = "Admin"
        }, "✅ Promoted to Admin successfully. Please logout and login in the app."));
    }

    // ═══════════════════════════════════════════════════════
    // 🆕 تشخيص سريع للتحقق من حالة الأدوار
    // ═══════════════════════════════════════════════════════
    [HttpGet("diagnose")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Diagnose(CancellationToken ct)
    {
        var rolesInDb = await _roles.Roles.Select(r => r.Name).ToListAsync(ct);
        var adminExists = await _roles.RoleExistsAsync(RoleNames.Admin);

        string? currentUserEmail = null;
        List<string>? currentUserRoles = null;

        if (_me.UserId.HasValue)
        {
            var currentUser = await _users.FindByIdAsync(_me.UserId.Value.ToString());
            if (currentUser != null)
            {
                currentUserEmail = currentUser.Email;
                currentUserRoles = (await _users.GetRolesAsync(currentUser)).ToList();
            }
        }

        var usersWithAdmin = await _db.UserRoles
            .Where(ur => _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Admin))
            .CountAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            rolesInDatabase = rolesInDb,
            adminRoleExists = adminExists,
            currentUserId = _me.UserId,
            currentUserEmail,
            currentUserRoles,
            totalAdmins = usersWithAdmin
        }));
    }

    // ═══════════════════════════════════════════════════════
    // 🆕 ترقية مباشرة بـ email (لا تحتاج Authorization header)
    // ═══════════════════════════════════════════════════════
    [HttpPost("force-promote")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForcePromote(
        [FromHeader(Name = "X-Setup-Key")] string? setupKey,
        [FromQuery] string email,
        CancellationToken ct)
    {
        var expectedKey = _cfg["Setup:SecretKey"] ?? DefaultSetupKey;
        if (setupKey != expectedKey)
            return Unauthorized(ApiResponse<object>.Fail("Invalid setup key."));

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.Fail("Email is required."));

        var user = await _users.FindByEmailAsync(email);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail($"User with email '{email}' not found."));

        // تأكد من وجود دور Admin
        if (!await _roles.RoleExistsAsync(RoleNames.Admin))
            await _roles.CreateAsync(new IdentityRole<Guid>(RoleNames.Admin));

        // تحقق من الدور الحالي
        if (await _users.IsInRoleAsync(user, RoleNames.Admin))
            return Ok(ApiResponse<object>.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                role = "Admin",
                alreadyAdmin = true
            }, "User is already Admin."));

        // أضف الدور
        var addResult = await _users.AddToRoleAsync(user, RoleNames.Admin);
        if (!addResult.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(
                "Failed: " + string.Join(", ", addResult.Errors.Select(e => e.Description))));

        // تحقق نهائي
        var userRoles = await _users.GetRolesAsync(user);
        if (!userRoles.Contains(RoleNames.Admin))
            return BadRequest(ApiResponse<object>.Fail("Role not persisted."));

        return Ok(ApiResponse<object>.Ok(new
        {
            userId = user.Id,
            email = user.Email,
            roles = userRoles,
            role = "Admin"
        }, "✅ Force promoted successfully."));
    }

    // ═══════════════════════════════════════════════════════
    // حذف كتب Open Library
    // ═══════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════
    // whoami (للتحقق من الحالة الحقيقية)
    // ═══════════════════════════════════════════════════════
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