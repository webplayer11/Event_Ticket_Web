using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OrderCode { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public Guid EventId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "VND";

    public OrderStatus Status { get; set; }
        = OrderStatus.PendingPayment;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}