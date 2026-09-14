using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class EventApprovalHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public EventStatus FromStatus { get; set; }

    public EventStatus ToStatus { get; set; }

    public Guid ActionByUserId { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}