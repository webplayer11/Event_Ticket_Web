namespace EventGO.Application.Reservations;

public enum InventoryReservationResult
{
    InsufficientInventory = 0,
    Success = 1
}

public enum ReservationTransitionResult
{
    NotApplied = 0,
    Applied = 1,
    AlreadyApplied = 2,
    Expired = 3,
    InventoryInvariantViolation = 4
}

public interface IReservationDatabaseOperations
{
    Task<InventoryReservationResult> ReserveInventoryAsync(
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<ReservationTransitionResult> ExpireReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<ReservationTransitionResult> CancelReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<ReservationTransitionResult> ConvertReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> LockActiveReservationForOrderAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
