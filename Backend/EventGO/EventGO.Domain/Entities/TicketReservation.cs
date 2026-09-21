using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class TicketReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderItemId { get; set; }

    public int Quantity { get; set; }

    public ReservationStatus Status { get; set; }
        = ReservationStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}