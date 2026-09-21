using EventGO.Application.Authentication.Dtos;

namespace EventGO.Application.Authentication;

public interface IAuthService
{
    Task<AuthOperationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> LogoutAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult<UserResponse>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}
