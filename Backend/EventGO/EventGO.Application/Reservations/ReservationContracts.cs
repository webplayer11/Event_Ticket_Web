using System.ComponentModel.DataAnnotations;
using EventGO.Domain.Enums;

namespace EventGO.Application.Reservations;

public sealed class CreateReservationRequest
{
    public Guid EventId { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<CreateReservationItemRequest> Items { get; init; }
        = Array.Empty<CreateReservationItemRequest>();
}

public sealed class CreateReservationItemRequest
{
    public Guid TicketTypeId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed record ReservationItemResponse(
    Guid Id,
    Guid TicketTypeId,
    int Quantity,
    decimal UnitPriceSnapshot);

public sealed record ReservationResponse(
    Guid Id,
    Guid UserId,
    Guid EventId,
    ReservationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReservationItemResponse> Items);

public enum ReservationError
{
    None,
    InvalidRequest,
    InvalidUser,
    EventNotFound,
    EventNotSaleable,
    TicketTypeNotFound,
    TicketTypeNotForEvent,
    TicketTypeNotOnSale,
    QuantityLimitExceeded,
    InventoryInsufficient,
    ConcurrencyConflict,
    NotFound,
    ConfigurationMissing
}

public sealed record ReservationResult(
    ReservationError Error,
    ReservationResponse? Reservation = null);

public interface IReservationService
{
    Task<ReservationResult> CreateAsync(
        Guid userId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationResult> GetAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ReservationResult> CancelAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IReservationExpirationService
{
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}
