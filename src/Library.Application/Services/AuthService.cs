using System.Security.Claims;
using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Library.Infrastructure.Email;
using Library.Infrastructure.Identity;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwtSettings;
    private readonly IEnumerable<IExternalAuthProvider> _externalProviders;
    private readonly IEmailService _email;
    private readonly ILogger<AuthService> _log;

    public AuthService(
        UserManager<ApplicationUser> users,
        IJwtTokenService jwt,
        AppDbContext db,
        IOptions<JwtSettings> jwtSettings,
        IEnumerable<IExternalAuthProvider> externalProviders,
        IEmailService email,
        ILogger<AuthService> log)
    {
        _users = users;
        _jwt = jwt;
        _db = db;
        _jwtSettings = jwtSettings.Value;
        _externalProviders = externalProviders;
        _email = email;
        _log = log;
    }

    // ═══════════════════════ Register ═══════════════════════
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (await _users.FindByEmailAsync(req.Email) is not null)
            return Result<AuthResponse>.Fail("Email already registered.");

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Provider = AuthProvider.Local
        };

        var res = await _users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            return Result<AuthResponse>.Fail("Registration failed",
                res.Errors.Select(e => e.Description).ToArray());

        await _users.AddToRoleAsync(user, RoleNames.User);
        _log.LogInformation("User registered: {Email}", user.Email);

        return Result<AuthResponse>.Ok(await IssueTokensAsync(user, ct), "Registered successfully");
    }

    // ═══════════════════════ Login ═══════════════════════
    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Fail("Invalid credentials.");

        if (!await _users.CheckPasswordAsync(user, req.Password))
            return Result<AuthResponse>.Fail("Invalid credentials.");

        _log.LogInformation("User login: {Email}", user.Email);
        return Result<AuthResponse>.Ok(await IssueTokensAsync(user, ct), "Logged in");
    }

    // ═══════════════════════ Refresh ═══════════════════════
    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (stored is null || !stored.IsActive)
            return Result<AuthResponse>.Fail("Invalid or expired refresh token.");

        stored.RevokedAt = DateTime.UtcNow;
        return Result<AuthResponse>.Ok(await IssueTokensAsync(stored.User, ct, stored), "Refreshed");
    }

    // ═══════════════════════ Logout ═══════════════════════
    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken, ct);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Result.Ok("Logged out");
    }

    // ═══════════════════════ Forgot Password ═══════════════════════
    public async Task<Result<object>> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            // لا تكشف وجود البريد
            return Result<object>.Ok(new { }, "If email exists, reset link sent.");
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        _log.LogInformation("Password reset requested for {Email}", email);

        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);

        var appLink = $"smartlibrary://reset-password?email={encodedEmail}&token={encodedToken}";
        var webLink = $"https://smartlib-api.runasp.net/reset-password?email={encodedEmail}&token={encodedToken}";

        var htmlBody = $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head><meta charset=""UTF-8""></head>
<body style=""font-family: Arial, Tahoma, sans-serif; background: #f5f5f5; margin: 0; padding: 20px;"">
  <div style=""max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; padding: 30px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);"">
    <div style=""text-align: center; margin-bottom: 24px;"">
      <div style=""width: 64px; height: 64px; background: #1DB9A8; border-radius: 16px; margin: 0 auto; line-height: 64px; color: white; font-size: 32px;"">📚</div>
      <h1 style=""color: #1DB9A8; margin: 16px 0 8px 0; font-size: 24px;"">Smart Library</h1>
    </div>

    <h2 style=""color: #333; font-size: 20px;"">إعادة تعيين كلمة المرور</h2>

    <p style=""color: #555; font-size: 15px; line-height: 1.6;"">عزيزي <strong>{user.FullName}</strong>,</p>

    <p style=""color: #555; font-size: 15px; line-height: 1.6;"">
      لقد طلبت إعادة تعيين كلمة المرور لحسابك في <strong>Smart Library</strong>.
    </p>

    <p style=""color: #555; font-size: 15px; line-height: 1.6;"">
      اضغط على الزر التالي لإعادة التعيين:
    </p>

    <div style=""text-align: center; margin: 30px 0;"">
      <a href=""{appLink}"" style=""background: #1DB9A8; color: white; padding: 14px 40px; text-decoration: none; border-radius: 10px; font-weight: bold; display: inline-block; font-size: 16px;"">
        إعادة تعيين كلمة المرور
      </a>
    </div>

    <p style=""color: #888; font-size: 13px; line-height: 1.6;"">
      إذا لم يعمل الزر، استخدم هذا الرابط:
    </p>
    <p style=""word-break: break-all; background: #f9f9f9; padding: 12px; border-radius: 6px; color: #1DB9A8; font-size: 12px;"">{webLink}</p>

    <hr style=""border: none; border-top: 1px solid #eee; margin: 30px 0;"" />

    <p style=""color: #999; font-size: 12px; line-height: 1.6;"">
      ⚠️ الرابط صالح لمرة واحدة فقط.<br/>
      إذا لم تطلب هذا، يمكنك تجاهل الرسالة بأمان.
    </p>

    <p style=""color: #999; font-size: 12px;"">
      شكراً،<br/>
      فريق Smart Library
    </p>
  </div>
</body>
</html>";

        try
        {
            await _email.SendAsync(
                email,
                "إعادة تعيين كلمة المرور - Smart Library",
                htmlBody,
                ct);

            return Result<object>.Ok(new { email }, "Reset link sent to your email.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send reset email to {Email}", email);
            return Result<object>.Fail("Failed to send reset email. Try again later.");
        }
    }

    // ═══════════════════════ Reset Password ═══════════════════════
    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null) return Result.Fail("Invalid request.");

        var res = await _users.ResetPasswordAsync(user, req.Token, req.NewPassword);
        return res.Succeeded
            ? Result.Ok("Password reset")
            : Result.Fail("Failed", res.Errors.Select(e => e.Description).ToArray());
    }

    // ═══════════════════════ Confirm Email ═══════════════════════
    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(req.UserId);
        if (user is null) return Result.Fail("Invalid user.");

        var res = await _users.ConfirmEmailAsync(user, req.Token);
        return res.Succeeded
            ? Result.Ok("Email confirmed")
            : Result.Fail("Failed", res.Errors.Select(e => e.Description).ToArray());
    }

    // ═══════════════════════ Google Login ═══════════════════════
    public Task<Result<AuthResponse>> GoogleLoginAsync(string idToken, CancellationToken ct = default)
        => ExternalLoginAsync(idToken, AuthProvider.Google, ct);

    // ═══════════════════════ Facebook Login ═══════════════════════
    public Task<Result<AuthResponse>> FacebookLoginAsync(string accessToken, CancellationToken ct = default)
        => ExternalLoginAsync(accessToken, AuthProvider.Facebook, ct);

    // ═══════════════════════ External Login ═══════════════════════
    private async Task<Result<AuthResponse>> ExternalLoginAsync(
        string token, AuthProvider provider, CancellationToken ct)
    {
        // ✅ الآن يجد المزود الصحيح (Google أو Facebook)
        var impl = _externalProviders.FirstOrDefault(p =>
            p.GetType().Name.Contains(provider.ToString(), StringComparison.OrdinalIgnoreCase));

        if (impl is null)
        {
            _log.LogError("External provider {Provider} not registered", provider);
            return Result<AuthResponse>.Fail($"{provider} login not available.");
        }

        var info = await impl.ValidateAsync(token, ct);
        if (info is null)
        {
            _log.LogWarning("{Provider} token validation failed", provider);
            return Result<AuthResponse>.Fail("Invalid external token.");
        }

        var user = await _users.FindByEmailAsync(info.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = info.Email,
                Email = info.Email,
                EmailConfirmed = true,
                FullName = info.FullName,
                Provider = provider,
                ExternalProviderId = info.ProviderId
            };
            var res = await _users.CreateAsync(user);
            if (!res.Succeeded)
                return Result<AuthResponse>.Fail("External signup failed",
                    res.Errors.Select(e => e.Description).ToArray());

            await _users.AddToRoleAsync(user, RoleNames.User);
            _log.LogInformation("New {Provider} user created: {Email}", provider, user.Email);
        }

        return Result<AuthResponse>.Ok(await IssueTokensAsync(user, ct), "Logged in");
    }

    // ═══════════════════════ Issue Tokens ═══════════════════════
    private async Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user, CancellationToken ct, RefreshToken? previous = null)
    {
        var (access, exp) = await _jwt.GenerateAccessTokenAsync(user);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(refresh);
        if (previous is not null) previous.ReplacedByToken = refresh.Token;
        await _db.SaveChangesAsync(ct);

        var dto = new UserDto(
            user.Id, user.FullName, user.Email!, user.ProfileImage,
            user.Provider.ToString(), user.IsActive, user.StorageUsed, user.StorageLimit);

        return new AuthResponse(access, refresh.Token, exp, dto);
    }
}