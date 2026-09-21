namespace EventGO.Domain.Entities;

public class TicketType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "VND";

    public int TotalQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public int SoldQuantity { get; set; }

    public int MaxQuantityPerOrder { get; set; } = 10;

    public DateTimeOffset SaleStartsAt { get; set; }

    public DateTimeOffset SaleEndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}