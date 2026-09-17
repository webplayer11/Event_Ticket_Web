using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using EventGO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<(bool Succeeded, string[] Errors)> RegisterAsync(
        RegisterRequest request)
    {
        var email = request.Email.Trim();
        var fullName = request.FullName.Trim();

        if (fullName.Length < 2)
        {
            return (false, ["Họ tên phải có ít nhất 2 ký tự."]);
        }

        if (request.Password != request.ConfirmPassword)
        {
            return (false, ["Mật khẩu xác nhận không khớp."]);
        }

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return (false, ["Không thể đăng ký với email này."]);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true
        };

        IdentityResult result;

        try
        {
            result = await _userManager.CreateAsync(
                user,
                request.Password);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql
                && (sql.Number == 2601 || sql.Number == 2627))
        {
            // Xử lý hai yêu cầu đăng ký trùng email cùng lúc.
            return (false, ["Không thể đăng ký với email này."]);
        }

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(error => error.Description)
                .ToArray();

            return (false, errors);
        }

        return (true, Array.Empty<string>());
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return null;
        }

        return _jwtTokenService.CreateToken(user);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt
        };
    }
}