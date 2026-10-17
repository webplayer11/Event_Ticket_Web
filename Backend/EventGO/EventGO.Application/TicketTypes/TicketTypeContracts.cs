using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.TicketTypes;

public class SaveTicketTypeRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string Currency { get; set; } = "VND";
    public int TotalQuantity { get; set; }
    public int MaxQuantityPerOrder { get; set; }
    public DateTimeOffset SaleStartsAt { get; set; }
    public DateTimeOffset SaleEndsAt { get; set; }
}

public sealed class UpdateTicketTypeStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }
}

public sealed record TicketTypeResponse(Guid Id, Guid EventId, string Name,
    string Description, decimal Price, string Currency, int TotalQuantity,
    int AvailableQuantity, int MaxQuantityPerOrder, DateTimeOffset SaleStartsAt,
    DateTimeOffset SaleEndsAt, bool IsActive);

public enum TicketTypeError
{
    None, EventNotFound, NotFound, Forbidden, Invalid, EventNotEditable,
    PriceLocked, InventoryCommitted, Conflict, CurrencyMismatch, SaleWindowClosed
}

public sealed record TicketTypeResult<T>(T? Data, TicketTypeError Error = TicketTypeError.None);

public interface ITicketTypeService
{
    Task<TicketTypeResult<TicketTypeResponse>> CreateAsync(Guid eventId, Guid userId, SaveTicketTypeRequest request, CancellationToken cancellationToken = default);
    Task<TicketTypeResult<List<TicketTypeResponse>>> GetAsync(Guid eventId, Guid? userId, CancellationToken cancellationToken = default);
    Task<TicketTypeResult<TicketTypeResponse>> UpdateAsync(Guid id, Guid userId, SaveTicketTypeRequest request, CancellationToken cancellationToken = default);
    Task<TicketTypeResult<TicketTypeResponse>> SetStatusAsync(Guid id, Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
