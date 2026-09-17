using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string PaymentReference { get; set; } = string.Empty;

    public string? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentStatus Status { get; set; }
        = PaymentStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? PaidAt { get; set; }

    public string? FailureReason { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}