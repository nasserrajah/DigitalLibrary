using Library.Domain.Constants;
using Library.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IConfiguration _config;
    private readonly ILogger<SetupController> _log;

    public SetupController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration config,
        ILogger<SetupController> log)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
        _log = log;
    }

   [HttpPost("promote")]
public async Task<IActionResult> PromoteToAdmin(
    [FromBody] PromoteRequest req,
    [FromServices] Library.Infrastructure.Identity.IJwtTokenService jwt,
    CancellationToken ct)
{
    var expectedKey = _config["Setup:PromotionKey"];
    if (string.IsNullOrWhiteSpace(expectedKey))
        return StatusCode(500, new { success = false, message = "Setup:PromotionKey غير مُهيّأ" });

    if (req.Key != expectedKey)
        return Unauthorized(new { success = false, message = "مفتاح الترقية غير صحيح" });

    var user = await _userManager.FindByEmailAsync(req.Email);
    if (user == null)
        return NotFound(new { success = false, message = $"المستخدم '{req.Email}' غير موجود" });

    if (!await _roleManager.RoleExistsAsync(RoleNames.Admin))
        await _roleManager.CreateAsync(new IdentityRole<Guid>(RoleNames.Admin));

    if (!await _userManager.IsInRoleAsync(user, RoleNames.Admin))
    {
        var r = await _userManager.AddToRoleAsync(user, RoleNames.Admin);
        if (!r.Succeeded)
            return BadRequest(new { success = false, errors = r.Errors.Select(e => e.Description) });
    }

    if (await _userManager.IsInRoleAsync(user, RoleNames.User))
        await _userManager.RemoveFromRoleAsync(user, RoleNames.User);

    _log.LogInformation("✅ {Email} promoted to Admin", user.Email);

    // 🆕 إنشاء توكن جديد بدور Admin
    var (accessToken, expiresAt) = await jwt.GenerateAccessTokenAsync(user);

    return Ok(new
    {
        success = true,
        message = $"✅ تم ترقية {user.Email} إلى Admin. التوكن الجديد جاهز.",
        data = new
        {
            email = user.Email,
            roles = await _userManager.GetRolesAsync(user),
            accessToken = accessToken,
            expiresAt = expiresAt
        }
    });
}
}

public record PromoteRequest(string Email, string Key);