using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class Reservation
{
    public const int IdempotencyKeyMaxLength = 128;

    public const int RequestHashLength = 64;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid EventId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClosedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
