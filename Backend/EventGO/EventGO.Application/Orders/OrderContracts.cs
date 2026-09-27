using System.ComponentModel.DataAnnotations;
using EventGO.Domain.Enums;

namespace EventGO.Application.Orders;

public sealed class CreateOrderRequest
{
    public Guid ReservationId { get; init; }

    [Required, MaxLength(150)]
    public string CustomerName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string CustomerEmail { get; init; } = string.Empty;

    [Required, MaxLength(30)]
    public string CustomerPhone { get; init; } = string.Empty;
}

public sealed record OrderItemResponse(
    Guid Id,
    Guid TicketTypeId,
    string TicketTypeName,
    decimal UnitPriceSnapshot,
    int Quantity);

public sealed record OrderResponse(
    Guid Id,
    Guid ReservationId,
    Guid EventId,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<OrderItemResponse> Items);

public enum OrderError
{
    None,
    ReservationNotFound,
    ReservationNotActive,
    ReservationExpired,
    AlreadyCreated,
    CurrencyMismatch,
    ConcurrencyConflict
}

public sealed record OrderResult(OrderError Error, OrderResponse? Order = null);

public interface IOrderService
{
    Task<OrderResult> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);
}
