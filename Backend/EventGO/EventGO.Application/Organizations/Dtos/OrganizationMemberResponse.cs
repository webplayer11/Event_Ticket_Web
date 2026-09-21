namespace EventGO.Application.Organizations.Dtos;

public class OrganizationMemberResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}