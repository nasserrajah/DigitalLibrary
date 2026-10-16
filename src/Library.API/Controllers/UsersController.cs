using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Library.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _svc;
    private readonly ICurrentUserService _me;
    private readonly IFileStorageService _storage;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<UsersController> _log;

    public UsersController(
        IUserService svc,
        ICurrentUserService me,
        IFileStorageService storage,
        UserManager<ApplicationUser> users,
        ILogger<UsersController> log)
    {
        _svc = svc;
        _me = me;
        _storage = storage;
        _users = users;
        _log = log;
    }

    // ═══════════════════════ GET /api/users/me ═══════════════════════
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
    {
        var r = await _svc.GetMeAsync(_me.UserId!.Value, ct);
        return r.Success ? Ok(ApiResponse<UserDto>.Ok(r.Data!, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    // ═══════════════════════ GET /api/users (Admin) ═══════════════════════
    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetAll(
        [FromQuery] PaginationQuery q, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<UserDto>>.Ok(await _svc.GetPagedAsync(q, ct)));

    // ═══════════════════════ PUT /api/users/{id}/toggle-active (Admin) ═══════════════════════
    [HttpPut("{id:guid}/toggle-active")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Toggle(Guid id, CancellationToken ct)
    {
        var r = await _svc.ToggleActiveAsync(id, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : NotFound(ApiResponse<object>.Fail(r.Message));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🆕 PUT /api/users/me/profile-image — رفع صورة شخصية
    // ═══════════════════════════════════════════════════════════════════
    [HttpPut("me/profile-image")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UploadProfileImage(
        IFormFile file,
        CancellationToken ct)
    {
        // 1) التحقق من الملف
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail(
                "Unsupported image format. Allowed: jpg, jpeg, png, webp."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail(
                "Image too large (max 5 MB)."));

        // 2) جلب المستخدم
        var userId = _me.UserId!.Value;
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        // 3) حذف الصورة القديمة إن كانت محلية (لا نحذف صور Google/Facebook)
        var oldImage = user.ProfileImage;
        if (!string.IsNullOrEmpty(oldImage) &&
            (oldImage.StartsWith("profiles") ||
             oldImage.StartsWith("profiles/") ||
             oldImage.StartsWith("profiles\\")))
        {
            try
            {
                await _storage.DeleteAsync(oldImage, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to delete old profile image: {Key}", oldImage);
            }
        }

        // 4) حفظ الصورة الجديدة
        try
        {
            await using var stream = file.OpenReadStream();
            var saved = await _storage.SaveAsync(stream, file.FileName, "profiles", ct);

            // 5) تحديث المستخدم
            user.ProfileImage = saved.StorageKey;
            user.UpdatedAt = DateTime.UtcNow;

            var res = await _users.UpdateAsync(user);
            if (!res.Succeeded)
            {
                // حاول حذف الملف المحفوظ عند فشل التحديث
                try { await _storage.DeleteAsync(saved.StorageKey, ct); } catch { }

                return BadRequest(ApiResponse<object>.Fail(
                    "Failed to update user profile.",
                    res.Errors.Select(e => e.Description).ToArray()));
            }

            _log.LogInformation("User {UserId} uploaded new profile image", userId);

            // 6) بناء DTO الرجوع
            var dto = new UserDto(
                user.Id,
                user.FullName,
                user.Email!,
                BuildFullImageUrl(user.ProfileImage, Request), 
                user.Provider.ToString(),
                user.IsActive,
                user.StorageUsed,
                user.StorageLimit);

            return Ok(ApiResponse<UserDto>.Ok(dto, "Profile image updated successfully."));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to upload profile image for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Fail(
                "Failed to upload image. Please try again."));
        }
    }

    private static string? BuildFullImageUrl(string? imageKey, HttpRequest request)
{
    if (string.IsNullOrEmpty(imageKey)) return null;

    // إذا كانت صورة خارجية (Google/Facebook)، أرجعها كما هي
    if (imageKey.StartsWith("http://") || imageKey.StartsWith("https://"))
        return imageKey;

    // صورة محلية → أضف Base URL
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var cleanKey = imageKey.TrimStart('/');
    return $"{baseUrl}/api/files/{cleanKey}";
}

    // ═══════════════════════════════════════════════════════════════════
    // 🆕 DELETE /api/users/me/profile-image — حذف الصورة الشخصية
    // ═══════════════════════════════════════════════════════════════════
    [HttpDelete("me/profile-image")]
    public async Task<ActionResult<ApiResponse<UserDto>>> DeleteProfileImage(CancellationToken ct)
    {
        var userId = _me.UserId!.Value;
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        // احذف الملف المحلي إن وُجد
        var oldImage = user.ProfileImage;
        if (!string.IsNullOrEmpty(oldImage) &&
            (oldImage.StartsWith("profiles") ||
             oldImage.StartsWith("profiles/") ||
             oldImage.StartsWith("profiles\\")))
        {
            try { await _storage.DeleteAsync(oldImage, ct); } catch { }
        }

        user.ProfileImage = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        _log.LogInformation("User {UserId} removed profile image", userId);

        var dto = new UserDto(
            user.Id,
            user.FullName,
            user.Email!,
            user.ProfileImage,
            user.Provider.ToString(),
            user.IsActive,
            user.StorageUsed,
            user.StorageLimit);

        return Ok(ApiResponse<UserDto>.Ok(dto, "Profile image removed."));
    }
}