using EventGO.Application.Authentication.Dtos;

namespace EventGO.Application.Authentication;

public interface IAuthService
{
    Task<(bool Succeeded, string[] Errors)> RegisterAsync(
        RegisterRequest request);

    Task<AuthResponse?> LoginAsync(LoginRequest request);

    Task<UserResponse?> GetCurrentUserAsync(Guid userId);
}