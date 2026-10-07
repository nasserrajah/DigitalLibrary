namespace Library.Application.DTOs.Auth;

public record RegisterRequest(string FullName, string Email, string Password, string? ConfirmPassword = null);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record ExternalAuthRequest(string IdToken, string? FullName = null);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record ConfirmEmailRequest(string UserId, string Token);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

public record UserDto(
    Guid Id, string FullName, string Email, string? ProfileImage,
    string Provider, bool IsActive, long StorageUsed, long StorageLimit);