using Library.Application.Common;
using Library.Application.DTOs.Auth;
using Library.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest req, CancellationToken ct)
    {
        var r = await _auth.RegisterAsync(req, ct);
        return r.Success ? Ok(ApiResponse<AuthResponse>.Ok(r.Data!, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest req, CancellationToken ct)
    {
        var r = await _auth.LoginAsync(req, ct);
        return r.Success ? Ok(ApiResponse<AuthResponse>.Ok(r.Data!, r.Message))
                         : Unauthorized(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshTokenRequest req, CancellationToken ct)
    {
        var r = await _auth.RefreshAsync(req.RefreshToken, ct);
        return r.Success ? Ok(ApiResponse<AuthResponse>.Ok(r.Data!, r.Message))
                         : Unauthorized(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout(RefreshTokenRequest req, CancellationToken ct)
    {
        var r = await _auth.LogoutAsync(req.RefreshToken, ct);
        return Ok(ApiResponse<object>.Ok(new { }, r.Message));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Forgot(ForgotPasswordRequest req, CancellationToken ct)
    {
        var r = await _auth.ForgotPasswordAsync(req.Email, ct);
        return Ok(ApiResponse<object>.Ok(new { }, r.Message));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Reset(ResetPasswordRequest req, CancellationToken ct)
    {
        var r = await _auth.ResetPasswordAsync(req, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Confirm(ConfirmEmailRequest req, CancellationToken ct)
    {
        var r = await _auth.ConfirmEmailAsync(req, ct);
        return r.Success ? Ok(ApiResponse<object>.Ok(new { }, r.Message))
                         : BadRequest(ApiResponse<object>.Fail(r.Message, r.Errors.ToArray()));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Google(ExternalAuthRequest req, CancellationToken ct)
    {
        var r = await _auth.GoogleLoginAsync(req.IdToken, ct);
        return r.Success ? Ok(ApiResponse<AuthResponse>.Ok(r.Data!, r.Message))
                         : Unauthorized(ApiResponse<object>.Fail(r.Message));
    }

    [HttpPost("facebook")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Facebook(ExternalAuthRequest req, CancellationToken ct)
    {
        var r = await _auth.FacebookLoginAsync(req.IdToken, ct);
        return r.Success ? Ok(ApiResponse<AuthResponse>.Ok(r.Data!, r.Message))
                         : Unauthorized(ApiResponse<object>.Fail(r.Message));
    }
}