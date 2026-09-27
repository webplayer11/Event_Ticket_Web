using System.Security.Cryptography;
using System.Text;
using EventGO.Application.Payments;
using EventGO.Application.Reservations;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Payments;

public sealed class PaymentCompletionService : IPaymentCompletionService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IReservationDatabaseOperations _databaseOperations;
    private readonly TimeProvider _timeProvider;

    public PaymentCompletionService(
        EventGoDbContext dbContext,
        IReservationDatabaseOperations databaseOperations,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _databaseOperations = databaseOperations;
        _timeProvider = timeProvider;
    }

    public async Task<PaymentCompletionResult> CompleteVerifiedPaymentAsync(
        Guid paymentId,
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return new(PaymentCompletionError.PaymentNotFound);
        }

        var order = await _dbContext.Orders
            .SingleAsync(x => x.Id == payment.OrderId, cancellationToken);
        var reservation = await _dbContext.Reservations.AsNoTracking()
            .SingleAsync(x => x.Id == order.ReservationId, cancellationToken);

        if (payment.Status == PaymentStatus.Succeeded
            && order.Status == OrderStatus.Paid
            && reservation.Status == ReservationStatus.Converted)
        {
            return new(PaymentCompletionError.None, AlreadyCompleted: true);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return new(PaymentCompletionError.PaymentNotPending);
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return new(PaymentCompletionError.OrderNotPayable);
        }

        if (payment.Amount != order.TotalAmount
            || !string.Equals(
                payment.Currency,
                order.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(PaymentCompletionError.PaymentAmountMismatch);
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return new(PaymentCompletionError.ReservationNotActive);
        }

        var now = _timeProvider.GetUtcNow();
        if (reservation.ExpiresAt <= now)
        {
            return new(PaymentCompletionError.LatePaymentPolicyUndefined);
        }

        var orderItems = await _dbContext.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var conversion = await _databaseOperations.ConvertReservationAsync(
            reservation.Id,
            now,
            cancellationToken);

        if (conversion != ReservationTransitionResult.Applied)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            return conversion switch
            {
                ReservationTransitionResult.Expired =>
                    new(PaymentCompletionError.LatePaymentPolicyUndefined),
                ReservationTransitionResult.InventoryInvariantViolation =>
                    new(PaymentCompletionError.InventoryConflict),
                ReservationTransitionResult.AlreadyApplied =>
                    await ResolveAlreadyConvertedAsync(paymentId, cancellationToken),
                _ => new(PaymentCompletionError.ReservationNotActive)
            };
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = now;
        payment.ProviderTransactionId = providerTransactionId;
        order.Status = OrderStatus.Paid;
        order.PaidAt = now;

        foreach (var orderItem in orderItems)
        {
            for (var index = 0; index < orderItem.Quantity; index++)
            {
                var ticketCode = $"TKT-{Guid.NewGuid():N}";
                _dbContext.Tickets.Add(new Ticket
                {
                    OrderItemId = orderItem.Id,
                    TicketCode = ticketCode,
                    QrTokenHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(ticketCode))),
                    Status = TicketStatus.Valid,
                    IssuedAt = now
                });
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PaymentCompletionError.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(PaymentCompletionError.ConcurrencyConflict);
        }
    }

    private async Task<PaymentCompletionResult> ResolveAlreadyConvertedAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var state = await _dbContext.Payments.AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Join(
                _dbContext.Orders.AsNoTracking(),
                payment => payment.OrderId,
                order => order.Id,
                (payment, order) => new { payment, order })
            .Join(
                _dbContext.Reservations.AsNoTracking(),
                value => value.order.ReservationId,
                reservation => reservation.Id,
                (value, reservation) => new
                {
                    PaymentStatus = value.payment.Status,
                    OrderStatus = value.order.Status,
                    ReservationStatus = reservation.Status
                })
            .SingleAsync(cancellationToken);

        return state.PaymentStatus == PaymentStatus.Succeeded
            && state.OrderStatus == OrderStatus.Paid
            && state.ReservationStatus == ReservationStatus.Converted
            ? new(PaymentCompletionError.None, AlreadyCompleted: true)
            : new(PaymentCompletionError.ConcurrencyConflict);
    }
}
