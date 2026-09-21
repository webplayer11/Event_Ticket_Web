using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderItemId { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public string QrTokenHash { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Valid;

    public DateTimeOffset IssuedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? UsedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}