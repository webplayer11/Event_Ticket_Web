using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class OrganizationMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public OrganizationMemberRole Role { get; set; }
        = OrganizationMemberRole.Staff;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset JoinedAt { get; set; }
        = DateTimeOffset.UtcNow;
}