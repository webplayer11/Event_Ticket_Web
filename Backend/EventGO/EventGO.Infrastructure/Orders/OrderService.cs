using EventGO.Application.Orders;
using EventGO.Application.Reservations;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Orders;

public sealed class OrderService : IOrderService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IReservationDatabaseOperations _databaseOperations;
    private readonly TimeProvider _timeProvider;

    public OrderService(
        EventGoDbContext dbContext,
        IReservationDatabaseOperations databaseOperations,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _databaseOperations = databaseOperations;
        _timeProvider = timeProvider;
    }

    public async Task<OrderResult> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.ReservationId && x.UserId == userId,
                cancellationToken);

        if (reservation is null)
        {
            return new(OrderError.ReservationNotFound);
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return new(OrderError.ReservationNotActive);
        }

        var now = _timeProvider.GetUtcNow();
        if (reservation.ExpiresAt <= now)
        {
            return new(OrderError.ReservationExpired);
        }

        if (await _dbContext.Orders.AsNoTracking()
            .AnyAsync(x => x.ReservationId == reservation.Id, cancellationToken))
        {
            return new(OrderError.AlreadyCreated);
        }

        var reservationItems = await _dbContext.ReservationItems
            .AsNoTracking()
            .Where(x => x.ReservationId == reservation.Id)
            .ToListAsync(cancellationToken);
        var ticketTypeIds = reservationItems.Select(x => x.TicketTypeId).ToArray();
        var ticketTypes = await _dbContext.TicketTypes
            .AsNoTracking()
            .Where(x => ticketTypeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var currencies = ticketTypes.Values
            .Select(x => x.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (currencies.Length != 1 || ticketTypes.Count != reservationItems.Count)
        {
            return new(OrderError.CurrencyMismatch);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var locked = await _databaseOperations.LockActiveReservationForOrderAsync(
            reservation.Id,
            now,
            cancellationToken);
        if (!locked)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await _dbContext.Reservations
                .AsNoTracking()
                .SingleAsync(x => x.Id == reservation.Id, cancellationToken);

            return new(current.ExpiresAt <= now
                ? OrderError.ReservationExpired
                : OrderError.ReservationNotActive);
        }

        var order = new Order
        {
            ReservationId = reservation.Id,
            EventId = reservation.EventId,
            UserId = userId,
            OrderCode = $"EGO-{Guid.NewGuid():N}",
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            Currency = currencies[0],
            TotalAmount = reservationItems.Sum(x => x.UnitPriceSnapshot * x.Quantity),
            Status = OrderStatus.PendingPayment,
            CreatedAt = now,
            ExpiresAt = reservation.ExpiresAt
        };

        var orderItems = reservationItems.Select(item => new OrderItem
        {
            OrderId = order.Id,
            TicketTypeId = item.TicketTypeId,
            TicketTypeName = ticketTypes[item.TicketTypeId].Name,
            UnitPrice = item.UnitPriceSnapshot,
            Quantity = item.Quantity
        }).ToArray();

        _dbContext.Orders.Add(order);
        _dbContext.OrderItems.AddRange(orderItems);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            if (await _dbContext.Orders.AsNoTracking()
                .AnyAsync(x => x.ReservationId == reservation.Id, cancellationToken))
            {
                return new(OrderError.AlreadyCreated);
            }

            return new(OrderError.ConcurrencyConflict);
        }

        return new(OrderError.None, Map(order, orderItems));
    }

    private static OrderResponse Map(Order order, IEnumerable<OrderItem> items) =>
        new(
            order.Id,
            order.ReservationId,
            order.EventId,
            order.Status,
            order.TotalAmount,
            order.Currency,
            order.ExpiresAt,
            items.Select(x => new OrderItemResponse(
                x.Id,
                x.TicketTypeId,
                x.TicketTypeName,
                x.UnitPrice,
                x.Quantity)).ToArray());
}
