// IAuthService.cs
using Library.Application.Common;
using Library.Application.DTOs.Auth;

namespace Library.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<object>> ForgotPasswordAsync(string email, CancellationToken ct = default);    Task<Result> ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> GoogleLoginAsync(string idToken, CancellationToken ct = default);
    Task<Result<AuthResponse>> FacebookLoginAsync(string accessToken, CancellationToken ct = default);
}
