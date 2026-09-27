namespace EventGO.Application.Authentication.Dtos;

public class UserResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public IReadOnlyList<string> SystemRoles { get; set; }
        = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; set; }
}
