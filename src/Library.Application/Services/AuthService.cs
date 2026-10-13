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

        // ═══════════ 1. إنشاء كلمة مرور جديدة مؤقتة ═══════════
        var newPassword = GenerateTemporaryPassword();

        // ═══════════ 2. إعادة تعيين كلمة المرور في Identity ═══════════
        var resetToken = await _users.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _users.ResetPasswordAsync(user, resetToken, newPassword);

        if (!resetResult.Succeeded)
        {
            _log.LogError("Failed to reset password for {Email}: {Errors}",
                email, string.Join(", ", resetResult.Errors.Select(e => e.Description)));

            return Result<object>.Fail("Failed to reset password. Try again later.");
        }

        _log.LogInformation("New password generated for {Email}", email);

        // ═══════════ 3. إرسال البريد مع كلمة المرور الجديدة ═══════════
        var htmlBody = $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head><meta charset=""UTF-8""></head>
<body style=""margin:0; padding:0; background:#f4f4f4; font-family: Arial, sans-serif;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#f4f4f4; padding:20px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff; border-radius:8px; padding:40px; max-width:600px;"">
        <tr>
          <td align=""center"" style=""padding-bottom:30px; border-bottom:1px solid #eeeeee;"">
            <h1 style=""color:#1DB9A8; margin:0; font-size:28px;"">Smart Library</h1>
            <p style=""color:#888; margin:5px 0 0 0; font-size:13px;"">مكتبتك الرقمية</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:30px 0 0 0;"">
            <h2 style=""color:#333; font-size:20px; margin:0 0 20px 0;"">كلمة المرور الجديدة</h2>

            <p style=""color:#555; font-size:15px; line-height:1.6; margin:0 0 20px 0;"">
              مرحباً <strong>{user.FullName}</strong>,
            </p>

            <p style=""color:#555; font-size:15px; line-height:1.6; margin:0 0 25px 0;"">
              لقد طلبت إعادة تعيين كلمة المرور. استخدم كلمة المرور التالية لتسجيل الدخول:
            </p>

            <div style=""background:#f0faf8; border:2px dashed #1DB9A8; border-radius:10px; padding:25px; text-align:center; margin:0 0 25px 0;"">
              <p style=""color:#888; font-size:13px; margin:0 0 10px 0;"">كلمة المرور الجديدة</p>
              <div style=""font-family: 'Courier New', monospace; font-size:24px; font-weight:bold; color:#1DB9A8; letter-spacing:3px; word-break:break-all;"">
                {newPassword}
              </div>
            </div>

            <p style=""color:#555; font-size:15px; line-height:1.6; margin:0 0 15px 0;"">
              انسخها والصقها في شاشة تسجيل الدخول.
            </p>

            <p style=""color:#d9534f; font-size:14px; line-height:1.6; margin:0 0 20px 0; padding:15px; background:#fdf2f2; border-radius:6px;"">
              🔒 <strong>مهم:</strong> بعد تسجيل الدخول، يُفضّل تغيير كلمة المرور إلى كلمة تتذكرها.
            </p>

            <hr style=""border:none; border-top:1px solid #eeeeee; margin:30px 0;"">

            <p style=""color:#999; font-size:12px; line-height:1.6; margin:0 0 10px 0;"">
              إذا لم تطلب هذا، يمكنك تجاهل الرسالة، لكن كلمة مرورك تغيّرت فعلاً.
              في هذه الحالة، تواصل مع الدعم.
            </p>

            <p style=""color:#999; font-size:12px; margin:20px 0 0 0;"">
              شكراً،<br>
              فريق Smart Library
            </p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>";

        try
        {
            await _email.SendAsync(
                email,
                "كلمة المرور الجديدة - Smart Library",
                htmlBody,
                ct);

            return Result<object>.Ok(
                new { email },
                "New password sent to your email.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send new password email to {Email}", email);

            // ⚠️ مهم: إذا فشل الإرسال، أعد كلمة المرور الأصلية؟ لا يمكن. 
            // لكن على الأقل لا نترك المستخدم بلا كلمة مرور — سنعرض الخطأ بوضوح.
            var detail = ex.InnerException?.Message ?? ex.Message;
            return Result<object>.Fail($"Email failed: {detail}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // توليد كلمة مرور عشوائية قوية
    // ═══════════════════════════════════════════════════════════
    private static string GenerateTemporaryPassword()
    {
        // نضمن وجود: حرف كبير + صغير + رقم + رمز خاص
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";

        var random = new Random();
        var password = new char[10];

        // 2 أحرف كبيرة
        password[0] = upper[random.Next(upper.Length)];
        password[1] = upper[random.Next(upper.Length)];

        // 3 أحرف صغيرة
        password[2] = lower[random.Next(lower.Length)];
        password[3] = lower[random.Next(lower.Length)];
        password[4] = lower[random.Next(lower.Length)];

        // 3 أرقام
        password[5] = digits[random.Next(digits.Length)];
        password[6] = digits[random.Next(digits.Length)];
        password[7] = digits[random.Next(digits.Length)];

        // 2 رموز
        password[8] = symbols[random.Next(symbols.Length)];
        password[9] = symbols[random.Next(symbols.Length)];

        // خلط عشوائي
        return new string(password.OrderBy(_ => random.Next()).ToArray());
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