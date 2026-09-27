namespace EventGO.Domain.Entities;

public class ReservationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReservationId { get; set; }

    public Guid TicketTypeId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPriceSnapshot { get; set; }
}
