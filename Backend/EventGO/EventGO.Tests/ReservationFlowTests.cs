using EventGO.Application.Orders;
using EventGO.Application.Payments;
using EventGO.Application.Reservations;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Orders;
using EventGO.Infrastructure.Payments;
using EventGO.Infrastructure.Persistence;
using EventGO.Infrastructure.Reservations;
using EventGO.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventGO.Tests;

public sealed class ReservationFlowTests
{
    [Fact]
    public async Task Create_snapshots_price_and_rejects_insufficient_inventory()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 2, price: 500_000m);
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);

        var created = await service.CreateAsync(
            database.UserId,
            Key(),
            Request(database.EventId, database.TicketTypeId, 2));

        Assert.Equal(ReservationError.None, created.Error);
        Assert.Equal(500_000m, created.Reservation!.Items.Single().UnitPriceSnapshot);
        Assert.Equal(2, await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());

        var rejected = await service.CreateAsync(
            database.UserId,
            Key(),
            Request(database.EventId, database.TicketTypeId, 1));

        Assert.Equal(ReservationError.InventoryInsufficient, rejected.Error);
        Assert.Equal(2, await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_releases_inventory_once()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);
        var created = await service.CreateAsync(
            database.UserId, Key(), Request(database.EventId, database.TicketTypeId, 2));

        var first = await service.CancelAsync(created.Reservation!.Id, database.UserId);
        var second = await service.CancelAsync(created.Reservation.Id, database.UserId);

        Assert.Equal(ReservationStatus.Cancelled, first.Reservation!.Status);
        Assert.Equal(ReservationStatus.Cancelled, second.Reservation!.Status);
        Assert.Equal(0, await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Expiration_is_idempotent_and_releases_inventory_once()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        await using var database = await TestDatabase.CreateAsync(clock: clock);
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);
        var created = await service.CreateAsync(
            database.UserId, Key(), Request(database.EventId, database.TicketTypeId, 2));
        clock.Advance(TimeSpan.FromMinutes(6));

        var first = await service.ExpireDueAsync();
        var second = await service.ExpireDueAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(ReservationStatus.Expired,
            await context.Reservations.Select(x => x.Status).SingleAsync());
        Assert.Equal(0, await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Order_uses_reservation_price_snapshot_without_reserving_again()
    {
        await using var database = await TestDatabase.CreateAsync(price: 500_000m);
        await using var context = database.CreateContext();
        var reservationService = database.CreateReservationService(context);
        var created = await reservationService.CreateAsync(
            database.UserId, Key(), Request(database.EventId, database.TicketTypeId, 2));

        await context.TicketTypes.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Price, 600_000m));
        var orderService = database.CreateOrderService(context);
        var order = await orderService.CreateAsync(database.UserId, new CreateOrderRequest
        {
            ReservationId = created.Reservation!.Id,
            CustomerName = "Customer",
            CustomerEmail = "customer@example.com",
            CustomerPhone = "+84000000000"
        });

        Assert.Equal(OrderError.None, order.Error);
        Assert.Equal(500_000m, order.Order!.Items.Single().UnitPriceSnapshot);
        Assert.Equal(1_000_000m, order.Order.TotalAmount);
        Assert.Equal(2, await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Payment_success_converts_once_and_issues_tickets_once()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var reservation = await database.CreateReservationService(context).CreateAsync(
            database.UserId, Key(), Request(database.EventId, database.TicketTypeId, 2));
        var order = await database.CreateOrderService(context).CreateAsync(
            database.UserId,
            new CreateOrderRequest
            {
                ReservationId = reservation.Reservation!.Id,
                CustomerName = "Customer",
                CustomerEmail = "customer@example.com",
                CustomerPhone = "+84000000000"
            });
        var payment = new Payment
        {
            OrderId = order.Order!.Id,
            Provider = "verified-test-provider",
            PaymentReference = Guid.NewGuid().ToString("N"),
            Amount = order.Order.TotalAmount,
            Currency = order.Order.Currency,
            Status = PaymentStatus.Pending,
            RowVersion = [1]
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        var completion = database.CreatePaymentCompletionService(context);

        var first = await completion.CompleteVerifiedPaymentAsync(payment.Id, "provider-tx-1");
        var second = await completion.CompleteVerifiedPaymentAsync(payment.Id, "provider-tx-1");

        Assert.Equal(PaymentCompletionError.None, first.Error);
        Assert.True(second.AlreadyCompleted);
        var inventory = await context.TicketTypes.AsNoTracking().SingleAsync();
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(2, inventory.SoldQuantity);
        Assert.True(inventory.ReservedQuantity + inventory.SoldQuantity <= inventory.TotalQuantity);
        Assert.Equal(ReservationStatus.Converted,
            await context.Reservations.Select(x => x.Status).SingleAsync());
        Assert.Equal(2, await context.Tickets.CountAsync());
    }

    [Fact]
    public async Task Expiration_and_late_payment_do_not_mutate_inventory_twice()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        await using var database = await TestDatabase.CreateAsync(clock: clock);
        await using var setupContext = database.CreateContext();
        var reservation = await database.CreateReservationService(setupContext).CreateAsync(
            database.UserId, Key(), Request(database.EventId, database.TicketTypeId, 2));
        var order = await database.CreateOrderService(setupContext).CreateAsync(
            database.UserId,
            new CreateOrderRequest
            {
                ReservationId = reservation.Reservation!.Id,
                CustomerName = "Customer",
                CustomerEmail = "customer@example.com",
                CustomerPhone = "+84000000000"
            });
        var payment = new Payment
        {
            OrderId = order.Order!.Id,
            Provider = "verified-test-provider",
            PaymentReference = Guid.NewGuid().ToString("N"),
            Amount = order.Order.TotalAmount,
            Currency = order.Order.Currency,
            Status = PaymentStatus.Pending,
            RowVersion = [1]
        };
        setupContext.Payments.Add(payment);
        await setupContext.SaveChangesAsync();
        clock.Advance(TimeSpan.FromMinutes(6));

        await using var expirationContext = database.CreateContext();
        await using var paymentContext = database.CreateContext();
        var expirationTask = database.CreateReservationService(expirationContext).ExpireDueAsync();
        var paymentTask = database.CreatePaymentCompletionService(paymentContext)
            .CompleteVerifiedPaymentAsync(payment.Id, "late-provider-tx");
        await Task.WhenAll(expirationTask, paymentTask);

        var completion = await paymentTask;
        Assert.Contains(
            completion.Error,
            new[]
            {
                PaymentCompletionError.LatePaymentPolicyUndefined,
                PaymentCompletionError.ReservationNotActive
            });
        await using var assertionContext = database.CreateContext();
        var inventory = await assertionContext.TicketTypes.AsNoTracking().SingleAsync();
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(0, inventory.SoldQuantity);
        Assert.Equal(ReservationStatus.Expired,
            await assertionContext.Reservations.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Concurrent_reservations_for_last_ticket_only_allow_one()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 1);
        await using var contextA = database.CreateContext();
        await using var contextB = database.CreateContext();
        var serviceA = database.CreateReservationService(contextA);
        var serviceB = database.CreateReservationService(contextB);

        var results = await Task.WhenAll(
            serviceA.CreateAsync(
                database.UserId,
                Key(),
                Request(database.EventId, database.TicketTypeId, 1)),
            serviceB.CreateAsync(
                database.UserId,
                Key(),
                Request(database.EventId, database.TicketTypeId, 1)));

        Assert.Single(results, x => x.Error == ReservationError.None);
        Assert.Single(results, x => x.Error == ReservationError.InventoryInsufficient);
        await using var assertionContext = database.CreateContext();
        var inventory = await assertionContext.TicketTypes.AsNoTracking().SingleAsync();
        Assert.Equal(1, inventory.ReservedQuantity);
        Assert.True(inventory.ReservedQuantity + inventory.SoldQuantity <= inventory.TotalQuantity);
    }

    [Fact]
    public async Task Multiple_items_roll_back_every_inventory_update_when_one_is_insufficient()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 1);
        await using var context = database.CreateContext();
        var unavailableTicketTypeId = Guid.Parse(
            "ffffffff-ffff-ffff-ffff-ffffffffffff");
        context.TicketTypes.Add(new TicketType
        {
            Id = unavailableTicketTypeId,
            EventId = database.EventId,
            Name = "Sold out zone",
            Price = 300_000m,
            Currency = "VND",
            TotalQuantity = 0,
            MaxQuantityPerOrder = 10,
            SaleStartsAt = database.Clock.GetUtcNow().AddDays(-1),
            SaleEndsAt = database.Clock.GetUtcNow().AddDays(1),
            IsActive = true,
            RowVersion = [1]
        });
        await context.SaveChangesAsync();

        var result = await database.CreateReservationService(context).CreateAsync(
            database.UserId,
            Key(),
            new CreateReservationRequest
            {
                EventId = database.EventId,
                Items =
                [
                    new() { TicketTypeId = database.TicketTypeId, Quantity = 1 },
                    new() { TicketTypeId = unavailableTicketTypeId, Quantity = 1 }
                ]
            });

        Assert.Equal(ReservationError.InventoryInsufficient, result.Error);
        Assert.Equal(0, await context.Reservations.CountAsync());
        Assert.All(
            await context.TicketTypes.AsNoTracking().ToListAsync(),
            inventory => Assert.Equal(0, inventory.ReservedQuantity));
    }

    [Fact]
    public async Task Sequential_duplicate_returns_same_reservation_without_reserving_again()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 10);
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);
        const string idempotencyKey = "sequential-duplicate";
        var request = Request(database.EventId, database.TicketTypeId, 2);

        var first = await service.CreateAsync(database.UserId, idempotencyKey, request);
        var retry = await service.CreateAsync(database.UserId, idempotencyKey, request);

        Assert.Equal(ReservationError.None, first.Error);
        Assert.Equal(ReservationError.None, retry.Error);
        Assert.Equal(first.Reservation!.Id, retry.Reservation!.Id);
        Assert.Equal(1, await context.Reservations.CountAsync());
        Assert.Equal(1, await context.ReservationItems.CountAsync());
        Assert.Equal(
            2,
            await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Missing_empty_or_oversized_idempotency_key_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);
        var request = Request(database.EventId, database.TicketTypeId, 1);

        var missing = await service.CreateAsync(database.UserId, null, request);
        var empty = await service.CreateAsync(database.UserId, "   ", request);
        var oversized = await service.CreateAsync(
            database.UserId,
            new string('x', Reservation.IdempotencyKeyMaxLength + 1),
            request);

        Assert.All(
            new[] { missing, empty, oversized },
            result => Assert.Equal(
                ReservationError.InvalidIdempotencyKey,
                result.Error));
        Assert.Equal(0, await context.Reservations.CountAsync());
        Assert.Equal(
            0,
            await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Same_key_with_different_payload_is_rejected_without_side_effect()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 10);
        await using var context = database.CreateContext();
        var service = database.CreateReservationService(context);
        const string idempotencyKey = "payload-conflict";

        var first = await service.CreateAsync(
            database.UserId,
            idempotencyKey,
            Request(database.EventId, database.TicketTypeId, 2));
        var conflict = await service.CreateAsync(
            database.UserId,
            idempotencyKey,
            Request(database.EventId, database.TicketTypeId, 5));

        Assert.Equal(ReservationError.None, first.Error);
        Assert.Equal(ReservationError.IdempotencyConflict, conflict.Error);
        Assert.Equal(1, await context.Reservations.CountAsync());
        Assert.Equal(
            2,
            await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Different_keys_and_same_key_for_different_users_are_independent()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 10);
        await using var context = database.CreateContext();
        var secondUserId = Guid.NewGuid();
        context.Users.Add(new ApplicationUser
        {
            Id = secondUserId,
            UserName = "second-reservation-user",
            NormalizedUserName = "SECOND-RESERVATION-USER",
            Email = "second-reservation-user@example.com",
            NormalizedEmail = "SECOND-RESERVATION-USER@EXAMPLE.COM",
            FullName = "Second Reservation User",
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = database.CreateReservationService(context);
        var request = Request(database.EventId, database.TicketTypeId, 1);

        var first = await service.CreateAsync(database.UserId, "key-a", request);
        var differentKey = await service.CreateAsync(database.UserId, "key-b", request);
        var differentUser = await service.CreateAsync(secondUserId, "key-a", request);

        Assert.All(
            new[] { first, differentKey, differentUser },
            result => Assert.Equal(ReservationError.None, result.Error));
        Assert.Equal(3, await context.Reservations.CountAsync());
        Assert.Equal(
            3,
            await context.TicketTypes.Select(x => x.ReservedQuantity).SingleAsync());
    }

    [Fact]
    public async Task Failed_multi_item_create_does_not_complete_idempotency_claim()
    {
        await using var database = await TestDatabase.CreateAsync(totalQuantity: 1);
        await using var context = database.CreateContext();
        var unavailableTicketTypeId = Guid.NewGuid();
        context.TicketTypes.Add(new TicketType
        {
            Id = unavailableTicketTypeId,
            EventId = database.EventId,
            Name = "Temporarily unavailable zone",
            Price = 300_000m,
            Currency = "VND",
            TotalQuantity = 0,
            MaxQuantityPerOrder = 10,
            SaleStartsAt = database.Clock.GetUtcNow().AddDays(-1),
            SaleEndsAt = database.Clock.GetUtcNow().AddDays(1),
            IsActive = true,
            RowVersion = [1]
        });
        await context.SaveChangesAsync();
        const string idempotencyKey = "retry-after-rollback";
        var request = new CreateReservationRequest
        {
            EventId = database.EventId,
            Items =
            [
                new() { TicketTypeId = database.TicketTypeId, Quantity = 1 },
                new() { TicketTypeId = unavailableTicketTypeId, Quantity = 1 }
            ]
        };
        var service = database.CreateReservationService(context);

        var failed = await service.CreateAsync(database.UserId, idempotencyKey, request);
        await context.TicketTypes
            .Where(x => x.Id == unavailableTicketTypeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.TotalQuantity, 1));
        var retry = await service.CreateAsync(database.UserId, idempotencyKey, request);

        Assert.Equal(ReservationError.InventoryInsufficient, failed.Error);
        Assert.Equal(ReservationError.None, retry.Error);
        Assert.Equal(1, await context.Reservations.CountAsync());
        Assert.Equal(2, await context.ReservationItems.CountAsync());
        Assert.All(
            await context.TicketTypes.AsNoTracking().ToListAsync(),
            inventory => Assert.Equal(1, inventory.ReservedQuantity));
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private static CreateReservationRequest Request(Guid eventId, Guid ticketTypeId, int quantity) =>
        new()
        {
            EventId = eventId,
            Items = [new CreateReservationItemRequest
            {
                TicketTypeId = ticketTypeId,
                Quantity = quantity
            }]
        };
}

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path;
    private readonly DbContextOptions<EventGoDbContext> _contextOptions;
    private readonly ReservationOptions _reservationOptions;

    private TestDatabase(
        string path,
        DbContextOptions<EventGoDbContext> contextOptions,
        TestTimeProvider clock)
    {
        _path = path;
        _contextOptions = contextOptions;
        Clock = clock;
        _reservationOptions = new ReservationOptions
        {
            Duration = TimeSpan.FromMinutes(5),
            SweepInterval = TimeSpan.FromMinutes(1)
        };
    }

    public Guid UserId { get; } = Guid.NewGuid();
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid TicketTypeId { get; } = Guid.NewGuid();
    public TestTimeProvider Clock { get; }

    public static async Task<TestDatabase> CreateAsync(
        int totalQuantity = 10,
        decimal price = 500_000m,
        TestTimeProvider? clock = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"reservation-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = false,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
        var options = new DbContextOptionsBuilder<EventGoDbContext>()
            .UseSqlite(connectionString)
            .Options;
        var database = new TestDatabase(
            path,
            options,
            clock ?? new TestTimeProvider(DateTimeOffset.Parse("2026-09-20T00:00:00Z")));

        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();
        context.Users.Add(new ApplicationUser
        {
            Id = database.UserId,
            UserName = "reservation-test-user",
            NormalizedUserName = "RESERVATION-TEST-USER",
            Email = "reservation-test@example.com",
            NormalizedEmail = "RESERVATION-TEST@EXAMPLE.COM",
            FullName = "Reservation Test User",
            IsActive = true
        });
        context.Events.Add(new Event
        {
            Id = database.EventId,
            Status = EventStatus.Published,
            StartsAt = database.Clock.GetUtcNow().AddDays(1),
            EndsAt = database.Clock.GetUtcNow().AddDays(2),
            RowVersion = [1]
        });
        context.TicketTypes.Add(new TicketType
        {
            Id = database.TicketTypeId,
            EventId = database.EventId,
            Name = "VIP",
            Price = price,
            Currency = "VND",
            TotalQuantity = totalQuantity,
            MaxQuantityPerOrder = 10,
            SaleStartsAt = database.Clock.GetUtcNow().AddDays(-1),
            SaleEndsAt = database.Clock.GetUtcNow().AddDays(1),
            IsActive = true,
            RowVersion = [1]
        });
        await context.SaveChangesAsync();
        return database;
    }

    public EventGoDbContext CreateContext() => new TestEventGoDbContext(_contextOptions);

    public ReservationService CreateReservationService(EventGoDbContext context) =>
        new(
            context,
            new TestReservationDatabaseOperations(context),
            Clock,
            Options.Create(_reservationOptions));

    public OrderService CreateOrderService(EventGoDbContext context) =>
        new(context, new TestReservationDatabaseOperations(context), Clock);

    public PaymentCompletionService CreatePaymentCompletionService(EventGoDbContext context) =>
        new(context, new TestReservationDatabaseOperations(context), Clock);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class TestReservationDatabaseOperations
    : IReservationDatabaseOperations
{
    private readonly EventGoDbContext _dbContext;

    public TestReservationDatabaseOperations(EventGoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryReservationResult> ReserveInventoryAsync(
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.TicketTypes
            .Where(x => x.Id == ticketTypeId
                && quantity > 0
                && x.TotalQuantity - x.ReservedQuantity - x.SoldQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.ReservedQuantity,
                    x => x.ReservedQuantity + quantity),
                cancellationToken);

        return affected == 1
            ? InventoryReservationResult.Success
            : InventoryReservationResult.InsufficientInventory;
    }

    public Task<ReservationTransitionResult> ExpireReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            reservationId,
            ReservationStatus.Expired,
            now,
            requireExpired: true,
            convert: false,
            cancellationToken);

    public Task<ReservationTransitionResult> CancelReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            reservationId,
            ReservationStatus.Cancelled,
            now,
            requireExpired: false,
            convert: false,
            cancellationToken);

    public Task<ReservationTransitionResult> ConvertReservationAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            reservationId,
            ReservationStatus.Converted,
            now,
            requireExpired: false,
            convert: true,
            cancellationToken);

    public Task<bool> LockActiveReservationForOrderAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        _dbContext.Reservations.AsNoTracking().AnyAsync(
            x => x.Id == reservationId
                && x.Status == ReservationStatus.Active
                && x.ExpiresAt > now,
            cancellationToken);

    private async Task<ReservationTransitionResult> TransitionAsync(
        Guid reservationId,
        ReservationStatus targetStatus,
        DateTimeOffset now,
        bool requireExpired,
        bool convert,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var reservation = await _dbContext.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ReservationTransitionResult.NotApplied;
        }

        if (reservation.Status == targetStatus)
        {
            return ReservationTransitionResult.AlreadyApplied;
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return ReservationTransitionResult.NotApplied;
        }

        if (requireExpired && reservation.ExpiresAt > now)
        {
            return ReservationTransitionResult.NotApplied;
        }

        if (convert && reservation.ExpiresAt <= now)
        {
            return ReservationTransitionResult.Expired;
        }

        var items = await _dbContext.ReservationItems.AsNoTracking()
            .Where(x => x.ReservationId == reservationId)
            .OrderBy(x => x.TicketTypeId)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return ReservationTransitionResult.InventoryInvariantViolation;
        }

        foreach (var item in items)
        {
            var affected = convert
                ? await _dbContext.TicketTypes
                    .Where(x => x.Id == item.TicketTypeId
                        && x.ReservedQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity - item.Quantity)
                        .SetProperty(x => x.SoldQuantity, x => x.SoldQuantity + item.Quantity), cancellationToken)
                : await _dbContext.TicketTypes
                    .Where(x => x.Id == item.TicketTypeId
                        && x.ReservedQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        x => x.ReservedQuantity,
                        x => x.ReservedQuantity - item.Quantity), cancellationToken);

            if (affected != 1)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return ReservationTransitionResult.InventoryInvariantViolation;
            }
        }

        var statusUpdated = await _dbContext.Reservations
            .Where(x => x.Id == reservationId && x.Status == ReservationStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, targetStatus)
                .SetProperty(x => x.ClosedAt, now), cancellationToken);

        if (statusUpdated != 1)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return ReservationTransitionResult.NotApplied;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return ReservationTransitionResult.Applied;
    }
}

internal sealed class TestEventGoDbContext : EventGoDbContext
{
    public TestEventGoDbContext(DbContextOptions<EventGoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.IsConcurrencyToken && property.ClrType == typeof(byte[]))
                {
                    property.ValueGenerated = ValueGenerated.Never;
                }

                if (property.GetColumnType()?.Contains("(max)", StringComparison.OrdinalIgnoreCase) == true)
                {
                    property.SetColumnType("TEXT");
                }

                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan value) => _utcNow = _utcNow.Add(value);
}
