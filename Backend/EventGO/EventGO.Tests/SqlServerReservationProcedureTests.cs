using EventGO.Application.Reservations;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using EventGO.Infrastructure.Reservations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventGO.Tests;

public sealed class SqlServerReservationProcedureTests
{
    [Fact]
    public async Task Procedures_are_deployed_atomic_and_idempotent()
    {
        var serverConnection = Environment.GetEnvironmentVariable(
            "EVENTGO_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(serverConnection))
        {
            Assert.Skip(
                "Set EVENTGO_TEST_SQLSERVER to an isolated SQL Server instance.");
        }

        await using var database =
            await SqlServerProcedureTestDatabase.CreateAsync(serverConnection);

        await database.AssertProceduresExistAsync();
        await VerifyConcurrentReserveAsync(database);
        await VerifyMultipleItemRollbackAsync(database);
        await VerifyTransitionsAsync(database);
    }

    private static async Task VerifyConcurrentReserveAsync(
        SqlServerProcedureTestDatabase database)
    {
        var inventoryId = await database.AddInventoryAsync("Last ticket", 1);
        await using var contextA = database.CreateContext();
        await using var contextB = database.CreateContext();
        var operationA = new SqlServerReservationDatabaseOperations(contextA);
        var operationB = new SqlServerReservationDatabaseOperations(contextB);

        var results = await Task.WhenAll(
            operationA.ReserveInventoryAsync(inventoryId, 1),
            operationB.ReserveInventoryAsync(inventoryId, 1));

        Assert.Single(results, x => x == InventoryReservationResult.Success);
        Assert.Single(
            results,
            x => x == InventoryReservationResult.InsufficientInventory);

        await using var assertionContext = database.CreateContext();
        var inventory = await assertionContext.TicketTypes
            .AsNoTracking()
            .SingleAsync(x => x.Id == inventoryId);
        Assert.Equal(1, inventory.ReservedQuantity);
        Assert.True(
            inventory.ReservedQuantity >= 0
            && inventory.SoldQuantity >= 0
            && inventory.ReservedQuantity + inventory.SoldQuantity
                <= inventory.TotalQuantity);
    }

    private static async Task VerifyMultipleItemRollbackAsync(
        SqlServerProcedureTestDatabase database)
    {
        var availableId = await database.AddInventoryAsync("Available zone", 1);
        var unavailableId = await database.AddInventoryAsync("Sold out zone", 0);
        await using var context = database.CreateContext();
        var operations = new SqlServerReservationDatabaseOperations(context);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var first = await operations.ReserveInventoryAsync(availableId, 1);
        var second = await operations.ReserveInventoryAsync(unavailableId, 1);

        Assert.Equal(InventoryReservationResult.Success, first);
        Assert.Equal(InventoryReservationResult.InsufficientInventory, second);
        await transaction.RollbackAsync();

        await using var assertionContext = database.CreateContext();
        Assert.Equal(
            0,
            await assertionContext.TicketTypes
                .Where(x => x.Id == availableId)
                .Select(x => x.ReservedQuantity)
                .SingleAsync());
    }

    private static async Task VerifyTransitionsAsync(
        SqlServerProcedureTestDatabase database)
    {
        var expiredId = await database.AddReservationAsync(
            "Expiration zone",
            ReservationStatus.Active,
            expiresIn: TimeSpan.FromMinutes(-1));
        var cancelledId = await database.AddReservationAsync(
            "Cancellation zone",
            ReservationStatus.Active,
            expiresIn: TimeSpan.FromMinutes(5));
        var convertedId = await database.AddReservationAsync(
            "Conversion zone",
            ReservationStatus.Active,
            expiresIn: TimeSpan.FromMinutes(5));

        await using var context = database.CreateContext();
        var operations = new SqlServerReservationDatabaseOperations(context);
        var now = database.Now;

        Assert.Equal(
            ReservationTransitionResult.Applied,
            await operations.ExpireReservationAsync(expiredId, now));
        Assert.Equal(
            ReservationTransitionResult.AlreadyApplied,
            await operations.ExpireReservationAsync(expiredId, now));
        Assert.Equal(
            ReservationTransitionResult.Applied,
            await operations.CancelReservationAsync(cancelledId, now));
        Assert.Equal(
            ReservationTransitionResult.AlreadyApplied,
            await operations.CancelReservationAsync(cancelledId, now));
        Assert.Equal(
            ReservationTransitionResult.Applied,
            await operations.ConvertReservationAsync(convertedId, now));
        Assert.Equal(
            ReservationTransitionResult.AlreadyApplied,
            await operations.ConvertReservationAsync(convertedId, now));

        await using var assertionContext = database.CreateContext();
        var reservations = await assertionContext.Reservations
            .AsNoTracking()
            .Where(x => new[] { expiredId, cancelledId, convertedId }.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        Assert.Equal(ReservationStatus.Expired, reservations[expiredId].Status);
        Assert.Equal(ReservationStatus.Cancelled, reservations[cancelledId].Status);
        Assert.Equal(ReservationStatus.Converted, reservations[convertedId].Status);

        var inventories = await assertionContext.ReservationItems
            .Where(x => new[] { expiredId, cancelledId, convertedId }
                .Contains(x.ReservationId))
            .Join(
                assertionContext.TicketTypes,
                item => item.TicketTypeId,
                inventory => inventory.Id,
                (item, inventory) => new
                {
                    item.ReservationId,
                    inventory.ReservedQuantity,
                    inventory.SoldQuantity,
                    inventory.TotalQuantity
                })
            .ToDictionaryAsync(x => x.ReservationId);

        Assert.Equal(0, inventories[expiredId].ReservedQuantity);
        Assert.Equal(0, inventories[cancelledId].ReservedQuantity);
        Assert.Equal(0, inventories[convertedId].ReservedQuantity);
        Assert.Equal(2, inventories[convertedId].SoldQuantity);
        Assert.All(inventories.Values, inventory =>
        {
            Assert.True(inventory.ReservedQuantity >= 0);
            Assert.True(inventory.SoldQuantity >= 0);
            Assert.True(
                inventory.ReservedQuantity + inventory.SoldQuantity
                <= inventory.TotalQuantity);
        });
    }
}

internal sealed class SqlServerProcedureTestDatabase : IAsyncDisposable
{
    private readonly string _serverConnection;
    private readonly string _databaseName;
    private readonly DbContextOptions<EventGoDbContext> _options;
    private int _nameSequence;

    private SqlServerProcedureTestDatabase(
        string serverConnection,
        string databaseName,
        DbContextOptions<EventGoDbContext> options)
    {
        _serverConnection = serverConnection;
        _databaseName = databaseName;
        _options = options;
    }

    public DateTimeOffset Now { get; } =
        DateTimeOffset.Parse("2026-09-20T00:00:00Z");

    public Guid UserId { get; } = Guid.NewGuid();

    public Guid EventId { get; } = Guid.NewGuid();

    public static async Task<SqlServerProcedureTestDatabase> CreateAsync(
        string serverConnection)
    {
        var databaseName = $"EventGO_ReservationTests_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(serverConnection)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        };
        var options = new DbContextOptionsBuilder<EventGoDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;
        var database = new SqlServerProcedureTestDatabase(
            serverConnection,
            databaseName,
            options);

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        await database.SeedAsync(context);
        return database;
    }

    public EventGoDbContext CreateContext() => new(_options);

    public ReservationService CreateReservationService(EventGoDbContext context) =>
        new(
            context,
            new SqlServerReservationDatabaseOperations(context),
            new TestTimeProvider(Now),
            Options.Create(new ReservationOptions
            {
                Duration = TimeSpan.FromMinutes(5),
                SweepInterval = TimeSpan.FromMinutes(1)
            }));

    public async Task<Guid> AddInventoryAsync(string name, int totalQuantity)
    {
        await using var context = CreateContext();
        var inventory = NewInventory(name, totalQuantity);
        context.TicketTypes.Add(inventory);
        await context.SaveChangesAsync();
        return inventory.Id;
    }

    public async Task<Guid> AddReservationAsync(
        string name,
        ReservationStatus status,
        TimeSpan expiresIn)
    {
        await using var context = CreateContext();
        var inventory = NewInventory(name, totalQuantity: 5);
        inventory.ReservedQuantity = 2;
        var reservation = new Reservation
        {
            UserId = UserId,
            EventId = EventId,
            Status = status,
            CreatedAt = Now.AddMinutes(-2),
            ExpiresAt = Now.Add(expiresIn)
        };
        var item = new ReservationItem
        {
            ReservationId = reservation.Id,
            TicketTypeId = inventory.Id,
            Quantity = 2,
            UnitPriceSnapshot = inventory.Price
        };
        context.AddRange(inventory, reservation, item);
        await context.SaveChangesAsync();
        return reservation.Id;
    }

    public async Task AssertProceduresExistAsync()
    {
        await using var context = CreateContext();
        var names = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT [name] AS [Value]
                FROM sys.procedures
                WHERE [name] IN (
                    'ReserveInventory',
                    'ExpireReservation',
                    'CancelReservation',
                    'ConvertReservation')
                """)
            .ToListAsync();
        Assert.Equal(4, names.Count);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_databaseName.StartsWith(
            "EventGO_ReservationTests_",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsafe test database name.");
        }

        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_serverConnection)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}]
                    SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync(EventGoDbContext context)
    {
        var organization = new Organization
        {
            Name = "Reservation Tests",
            ContactEmail = "tests@example.com"
        };
        var category = new EventCategory
        {
            Name = "Tests",
            Slug = $"tests-{Guid.NewGuid():N}"
        };
        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = "reservation-tests",
            NormalizedUserName = "RESERVATION-TESTS",
            Email = "reservation-tests@example.com",
            NormalizedEmail = "RESERVATION-TESTS@EXAMPLE.COM",
            FullName = "Reservation Tests",
            IsActive = true
        };
        var eventEntity = new Event
        {
            Id = EventId,
            OrganizationId = organization.Id,
            CategoryId = category.Id,
            CreatedByUserId = user.Id,
            Title = "Reservation Procedure Tests",
            Slug = $"reservation-tests-{Guid.NewGuid():N}",
            StartsAt = Now.AddDays(1),
            EndsAt = Now.AddDays(2),
            Status = EventStatus.Published
        };
        context.AddRange(organization, category, user, eventEntity);
        await context.SaveChangesAsync();
    }

    private TicketType NewInventory(string name, int totalQuantity) => new()
    {
        EventId = EventId,
        Name = $"{name}-{++_nameSequence}",
        Price = 500_000m,
        Currency = "VND",
        TotalQuantity = totalQuantity,
        MaxQuantityPerOrder = 10,
        SaleStartsAt = Now.AddDays(-1),
        SaleEndsAt = Now.AddDays(1),
        IsActive = true
    };
}
