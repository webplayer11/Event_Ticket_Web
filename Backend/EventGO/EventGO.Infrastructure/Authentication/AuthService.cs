using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventGO.Infrastructure.Authentication;

public sealed class AuthService : IAuthService
{
    private const string InvalidResetMessage =
        "Không thể đặt lại mật khẩu bằng thông tin đã cung cấp.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly EventGoDbContext _dbContext;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        EventGoDbContext dbContext,
        IPasswordResetEmailSender passwordResetEmailSender,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
        _passwordResetEmailSender = passwordResetEmailSender;
        _logger = logger;
    }

    public async Task<AuthOperationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var fullName = request.FullName.Trim();

        if (fullName.Length < 2)
        {
            return AuthOperationResult.Failure(
                "Họ tên phải có ít nhất 2 ký tự.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return AuthOperationResult.Failure(
                "Mật khẩu xác nhận không khớp.");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return AuthOperationResult.Failure(
                "Không thể đăng ký với thông tin đã cung cấp.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true
        };

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var createResult = await _userManager.CreateAsync(
                user,
                request.Password);

            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureFromIdentity(createResult);
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                SystemRoles.Customer);

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(
                    "Could not assign the default system role during registration.");
                throw new InvalidOperationException(
                    "The default system role is unavailable.");
            }

            await transaction.CommitAsync(cancellationToken);
            return AuthOperationResult.Success();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthOperationResult.Failure(
                "Không thể đăng ký với thông tin đã cung cấp.");
        }
    }

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        return await _jwtTokenService.CreateTokenAsync(user);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return await MapUserAsync(user);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        try
        {
            await _passwordResetEmailSender.SendAsync(
                user.Email!,
                token,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            // Keep the public response neutral. Never log the token or email.
            _logger.LogError(
                exception,
                "Password reset email delivery failed for user {UserId}.",
                user.Id);
        }
    }

    public async Task<AuthOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return AuthOperationResult.Failure(InvalidResetMessage);
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);

        if (result.Succeeded)
        {
            // Identity rotates SecurityStamp when the password hash changes.
            return AuthOperationResult.Success();
        }

        if (result.Errors.Any(error =>
            string.Equals(
                error.Code,
                "InvalidToken",
                StringComparison.OrdinalIgnoreCase)))
        {
            return AuthOperationResult.Failure(InvalidResetMessage);
        }

        return FailureFromIdentity(result);
    }

    public async Task<AuthOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return AuthOperationResult.Failure("Không thể đổi mật khẩu.");
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (result.Succeeded)
        {
            // Identity rotates SecurityStamp when the password hash changes.
            return AuthOperationResult.Success();
        }

        if (result.Errors.Any(error =>
            string.Equals(
                error.Code,
                "PasswordMismatch",
                StringComparison.OrdinalIgnoreCase)))
        {
            return AuthOperationResult.Failure(
                "Mật khẩu hiện tại không chính xác.");
        }

        return FailureFromIdentity(result);
    }

    public async Task<bool> LogoutAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return false;
        }

        var result = await _userManager.UpdateSecurityStampAsync(user);
        return result.Succeeded;
    }

    public async Task<AuthOperationResult<UserResponse>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return AuthOperationResult<UserResponse>.Failure(
                "Không tìm thấy tài khoản đang hoạt động.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return AuthOperationResult<UserResponse>.Failure(
                result.Errors.Select(error => error.Description).ToArray());
        }

        return AuthOperationResult<UserResponse>.Success(
            await MapUserAsync(user));
    }

    private async Task<UserResponse> MapUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            SystemRoles = roles.Order(StringComparer.Ordinal).ToArray(),
            CreatedAt = user.CreatedAt
        };
    }

    private static AuthOperationResult FailureFromIdentity(
        IdentityResult result) =>
        AuthOperationResult.Failure(
            result.Errors.Select(error => error.Description).ToArray());

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception) =>
        exception.InnerException is SqlException sql
            && (sql.Number == 2601 || sql.Number == 2627);
}
