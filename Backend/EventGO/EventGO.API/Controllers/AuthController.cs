using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventGO.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Đăng ký không thành công.",
                errors = result.Errors
            });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Đăng ký thành công. Bạn có thể đăng nhập."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(
            request,
            cancellationToken);

        if (response is null)
        {
            return Unauthorized(new
            {
                message =
                    "Đăng nhập không thành công. " +
                    "Kiểm tra thông tin hoặc thử lại sau."
            });
        }

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentUserAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(user);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);

        return Accepted(new
        {
            message =
                "Nếu tài khoản tồn tại, hướng dẫn đặt lại mật khẩu sẽ được gửi."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Không thể đặt lại mật khẩu.",
                errors = result.Errors
            });
        }

        return Ok(new
        {
            message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại."
        });
    }

    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(
            userId,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Không thể đổi mật khẩu.",
                errors = result.Errors
            });
        }

        return Ok(new
        {
            message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var succeeded = await _authService.LogoutAsync(
            userId,
            cancellationToken);

        return succeeded ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.UpdateProfileAsync(
            userId,
            request,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return BadRequest(new
            {
                message = "Không thể cập nhật hồ sơ.",
                errors = result.Errors
            });
        }

        return Ok(result.Value);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out userId);
    }
}
