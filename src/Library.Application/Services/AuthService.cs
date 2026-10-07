using System.Security.Claims;
using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.Interfaces;
using Library.Domain.Constants;
using Library.Domain.Entities;
using Library.Domain.Enums;
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
    private readonly ILogger<AuthService> _log;

    public AuthService(UserManager<ApplicationUser> users,
        IJwtTokenService jwt, AppDbContext db,
        IOptions<JwtSettings> jwtSettings,
        IEnumerable<IExternalAuthProvider> externalProviders,
        ILogger<AuthService> log)
    { _users = users; _jwt = jwt; _db = db; _jwtSettings = jwtSettings.Value; _externalProviders = externalProviders; _log = log; }

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
            return Result<AuthResponse>.Fail("Registration failed", res.Errors.Select(e => e.Description).ToArray());

        await _users.AddToRoleAsync(user, RoleNames.User);
        _log.LogInformation("User registered: {Email}", user.Email);
        return Result<AuthResponse>.Ok(await IssueTokensAsync(user, ct), "Registered successfully");
    }

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

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);
        if (stored is null || !stored.IsActive)
            return Result<AuthResponse>.Fail("Invalid or expired refresh token.");

        stored.RevokedAt = DateTime.UtcNow;
        return Result<AuthResponse>.Ok(await IssueTokensAsync(stored.User, ct, stored), "Refreshed");
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken, ct);
        if (stored is not null) { stored.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
        return Result.Ok("Logged out");
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null) return Result.Ok("If email exists, reset link sent."); // don't leak
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        _log.LogInformation("Password reset token for {Email}: {Token}", email, token);
        return Result.Ok("If email exists, reset link sent.");
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null) return Result.Fail("Invalid request.");
        var res = await _users.ResetPasswordAsync(user, req.Token, req.NewPassword);
        return res.Succeeded ? Result.Ok("Password reset") : Result.Fail("Failed", res.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest req, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(req.UserId);
        if (user is null) return Result.Fail("Invalid user.");
        var res = await _users.ConfirmEmailAsync(user, req.Token);
        return res.Succeeded ? Result.Ok("Email confirmed") : Result.Fail("Failed", res.Errors.Select(e => e.Description).ToArray());
    }

    public Task<Result<AuthResponse>> GoogleLoginAsync(string idToken, CancellationToken ct = default)
        => ExternalLoginAsync(idToken, AuthProvider.Google, ct);

    public Task<Result<AuthResponse>> FacebookLoginAsync(string accessToken, CancellationToken ct = default)
        => ExternalLoginAsync(accessToken, AuthProvider.Facebook, ct);

    private async Task<Result<AuthResponse>> ExternalLoginAsync(string token, AuthProvider provider, CancellationToken ct)
    {
        // pick the correct provider by ordering (google first) — replaced by named resolution in prod
        var impl = _externalProviders.FirstOrDefault(p => p.GetType().Name.Contains(provider.ToString()))
                   ?? _externalProviders.First();
        var info = await impl.ValidateAsync(token, ct);
        if (info is null) return Result<AuthResponse>.Fail("Invalid external token.");

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
            if (!res.Succeeded) return Result<AuthResponse>.Fail("External signup failed", res.Errors.Select(e => e.Description).ToArray());
            await _users.AddToRoleAsync(user, RoleNames.User);
        }
        return Result<AuthResponse>.Ok(await IssueTokensAsync(user, ct), "Logged in");
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct, RefreshToken? previous = null)
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

        var roles = await _users.GetRolesAsync(user);
        var dto = new UserDto(user.Id, user.FullName, user.Email!, user.ProfileImage,
            user.Provider.ToString(), user.IsActive, user.StorageUsed, user.StorageLimit);
        return new AuthResponse(access, refresh.Token, exp, dto);
    }
}