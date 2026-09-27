using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EventGO.Application.Reservations;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Reservations;

public sealed class ReservationService
    : IReservationService, IReservationExpirationService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IReservationDatabaseOperations _databaseOperations;
    private readonly TimeProvider _timeProvider;
    private readonly ReservationOptions _options;

    public ReservationService(
        EventGoDbContext dbContext,
        IReservationDatabaseOperations databaseOperations,
        TimeProvider timeProvider,
        IOptions<ReservationOptions> options)
    {
        _dbContext = dbContext;
        _databaseOperations = databaseOperations;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<ReservationResult> CreateAsync(
        Guid userId,
        string? idempotencyKey,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.Duration <= TimeSpan.Zero)
        {
            return new(ReservationError.ConfigurationMissing);
        }

        var normalizedIdempotencyKey = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(normalizedIdempotencyKey)
            || normalizedIdempotencyKey.Length > Reservation.IdempotencyKeyMaxLength)
        {
            return new(ReservationError.InvalidIdempotencyKey);
        }

        if (userId == Guid.Empty
            || request.EventId == Guid.Empty
            || request.Items.Count == 0
            || request.Items.Any(x => x.TicketTypeId == Guid.Empty || x.Quantity <= 0)
            || request.Items.GroupBy(x => x.TicketTypeId).Any(x => x.Count() > 1))
        {
            return new(ReservationError.InvalidRequest);
        }

        var userIsActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!userIsActive)
        {
            return new(ReservationError.InvalidUser);
        }

        var requestHash = ComputeRequestHash(request);
        var existingResult = await FindExistingAsync(
            userId,
            normalizedIdempotencyKey,
            requestHash,
            cancellationToken);
        if (existingResult is not null)
        {
            return existingResult;
        }

        var now = _timeProvider.GetUtcNow();
        var eventState = await _dbContext.Events
            .AsNoTracking()
            .Where(x => x.Id == request.EventId)
            .Select(x => new { x.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (eventState is null)
        {
            return new(ReservationError.EventNotFound);
        }

        if (eventState.Status != EventStatus.Published)
        {
            return new(ReservationError.EventNotSaleable);
        }

        var requestedIds = request.Items.Select(x => x.TicketTypeId).ToArray();
        var ticketTypes = await _dbContext.TicketTypes
            .AsNoTracking()
            .Where(x => requestedIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var validationError = ValidateItems(request, ticketTypes, now);
        if (validationError != ReservationError.None)
        {
            return new(validationError);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var reservation = new Reservation
        {
            UserId = userId,
            EventId = request.EventId,
            IdempotencyKey = normalizedIdempotencyKey,
            RequestHash = requestHash,
            Status = ReservationStatus.Active,
            CreatedAt = now,
            ExpiresAt = now.Add(_options.Duration)
        };

        _dbContext.Reservations.Add(reservation);

        try
        {
            // This insert is the transaction-scoped idempotency claim. The unique
            // index serializes concurrent requests before inventory is mutated.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var duplicateResult = await FindExistingAsync(
                userId,
                normalizedIdempotencyKey,
                requestHash,
                cancellationToken);

            if (duplicateResult is null)
            {
                throw;
            }

            return duplicateResult;
        }

        foreach (var item in request.Items.OrderBy(x => x.TicketTypeId))
        {
            var reserveResult = await _databaseOperations.ReserveInventoryAsync(
                item.TicketTypeId,
                item.Quantity,
                cancellationToken);

            if (reserveResult != InventoryReservationResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                return new(ReservationError.InventoryInsufficient);
            }
        }

        var reservationItems = request.Items.Select(item =>
            new ReservationItem
            {
                ReservationId = reservation.Id,
                TicketTypeId = item.TicketTypeId,
                Quantity = item.Quantity,
                UnitPriceSnapshot = ticketTypes[item.TicketTypeId].Price
            }).ToArray();

        _dbContext.ReservationItems.AddRange(reservationItems);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(ReservationError.None, Map(reservation, reservationItems));
    }

    private async Task<ReservationResult?> FindExistingAsync(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId
                    && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        if (!string.Equals(
                reservation.RequestHash,
                requestHash,
                StringComparison.Ordinal))
        {
            return new(ReservationError.IdempotencyConflict);
        }

        var items = await _dbContext.ReservationItems
            .AsNoTracking()
            .Where(x => x.ReservationId == reservation.Id)
            .OrderBy(x => x.TicketTypeId)
            .ToListAsync(cancellationToken);

        return new(ReservationError.None, Map(reservation, items));
    }

    private static string ComputeRequestHash(CreateReservationRequest request)
    {
        var canonicalPayload = new StringBuilder()
            .Append(request.EventId.ToString("N"))
            .Append('|');

        foreach (var item in request.Items.OrderBy(x => x.TicketTypeId))
        {
            canonicalPayload
                .Append(item.TicketTypeId.ToString("N"))
                .Append(':')
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture))
                .Append(';');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload.ToString())));
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    public async Task<ReservationResult> GetAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == reservationId && x.UserId == userId,
                cancellationToken);

        if (reservation is null)
        {
            return new(ReservationError.NotFound);
        }

        if (reservation.Status == ReservationStatus.Active
            && reservation.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            var transition = await _databaseOperations.ExpireReservationAsync(
                reservation.Id,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            if (transition == ReservationTransitionResult.InventoryInvariantViolation)
            {
                return new(ReservationError.ConcurrencyConflict);
            }

            reservation = await _dbContext.Reservations
                .AsNoTracking()
                .SingleAsync(x => x.Id == reservationId, cancellationToken);
        }

        var items = await _dbContext.ReservationItems
            .AsNoTracking()
            .Where(x => x.ReservationId == reservationId)
            .ToListAsync(cancellationToken);

        return new(ReservationError.None, Map(reservation, items));
    }

    public async Task<ReservationResult> CancelAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == reservationId && x.UserId == userId,
                cancellationToken);

        if (reservation is null)
        {
            return new(ReservationError.NotFound);
        }

        if (reservation.Status == ReservationStatus.Active)
        {
            var transition = await _databaseOperations.CancelReservationAsync(
                reservationId,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            if (transition == ReservationTransitionResult.InventoryInvariantViolation)
            {
                return new(ReservationError.ConcurrencyConflict);
            }
        }

        return await GetAsync(reservationId, userId, cancellationToken);
    }

    public async Task<int> ExpireDueAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var dueIds = await _dbContext.Reservations
            .AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Active && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Select(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var id in dueIds)
        {
            var result = await _databaseOperations.ExpireReservationAsync(
                id,
                now,
                cancellationToken);
            if (result == ReservationTransitionResult.Applied)
            {
                expired++;
            }
        }

        return expired;
    }

    private static ReservationError ValidateItems(
        CreateReservationRequest request,
        IReadOnlyDictionary<Guid, TicketType> ticketTypes,
        DateTimeOffset now)
    {
        if (ticketTypes.Count != request.Items.Count)
        {
            return ReservationError.TicketTypeNotFound;
        }

        foreach (var item in request.Items)
        {
            var ticketType = ticketTypes[item.TicketTypeId];
            if (ticketType.EventId != request.EventId)
            {
                return ReservationError.TicketTypeNotForEvent;
            }

            if (!ticketType.IsActive
                || ticketType.SaleStartsAt > now
                || ticketType.SaleEndsAt <= now)
            {
                return ReservationError.TicketTypeNotOnSale;
            }

            if (item.Quantity > ticketType.MaxQuantityPerOrder)
            {
                return ReservationError.QuantityLimitExceeded;
            }
        }

        return ReservationError.None;
    }

    private static ReservationResponse Map(
        Reservation reservation,
        IEnumerable<ReservationItem> items) =>
        new(
            reservation.Id,
            reservation.UserId,
            reservation.EventId,
            reservation.Status,
            reservation.ExpiresAt,
            reservation.CreatedAt,
            items.Select(x => new ReservationItemResponse(
                x.Id,
                x.TicketTypeId,
                x.Quantity,
                x.UnitPriceSnapshot)).ToArray());
}
