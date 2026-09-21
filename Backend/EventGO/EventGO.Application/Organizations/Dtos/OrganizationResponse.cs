namespace EventGO.Application.Organizations.Dtos;

public class OrganizationResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MyRole { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}