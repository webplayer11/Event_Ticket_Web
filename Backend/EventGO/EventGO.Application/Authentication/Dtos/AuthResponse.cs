namespace EventGO.Application.Authentication.Dtos;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public DateTimeOffset ExpiresAt { get; set; }

    public UserResponse User { get; set; } = new();
}