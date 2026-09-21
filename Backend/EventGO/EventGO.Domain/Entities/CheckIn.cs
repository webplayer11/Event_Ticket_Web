namespace EventGO.Domain.Entities;

public class CheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Guid EventStaffAssignmentId { get; set; }

    public DateTimeOffset CheckedInAt { get; set; }
        = DateTimeOffset.UtcNow;

    public string? GateName { get; set; }

    public string? Note { get; set; }
}