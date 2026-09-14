namespace EventGO.Domain.Entities;

public class EventStaffAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public Guid OrganizationMemberId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset AssignedAt { get; set; }
        = DateTimeOffset.UtcNow;
}