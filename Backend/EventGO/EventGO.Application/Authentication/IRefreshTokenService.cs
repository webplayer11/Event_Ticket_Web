using System.ComponentModel.DataAnnotations;
using EventGO.Application.Authentication.Dtos;

namespace EventGO.Application.Authentication;

public sealed class RefreshTokenRequest
{
    [Required, StringLength(128, MinimumLength = 64)]
    public string RefreshToken { get; set; } = string.Empty;
}

public interface IRefreshTokenService
{
    Task<AuthResponse?> RefreshAsync(string token, CancellationToken cancellationToken = default);
}
