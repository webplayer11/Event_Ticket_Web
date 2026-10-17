using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventGO.Application.Reservations;
using EventGO.Application.TicketTypes;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Authorization;
using EventGO.Infrastructure.Persistence;
using EventGO.Infrastructure.TicketTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace EventGO.Tests;

public sealed class SqlServerTicketAndTokenTests
{
    private static string Server()
    {
        var server = Environment.GetEnvironmentVariable("EVENTGO_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server)) Assert.Skip("Set EVENTGO_TEST_SQLSERVER to an isolated SQL Server instance.");
        return server!;
    }

    [Fact]
    public async Task Concurrent_refresh_has_exactly_one_successor_and_migration_roundtrip_preserves_existing_data()
    {
        await using var database = await SqlServerProcedureTestDatabase.CreateAsync(Server());
        await using var context = database.CreateContext();
        var connection = context.Database.GetConnectionString()!;
        await using (var factory = new AuthApiFactory(connection))
        {
            using var client = factory.CreateClient();
            await factory.InitializeDatabaseAsync();
            var email = $"sql-refresh-{Guid.NewGuid():N}@example.com";
            var user = await factory.CreateUserAsync(email, "Valid!Pass123");
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Valid!Pass123" });
            login.EnsureSuccessStatusCode();
            var body = await login.Content.ReadFromJsonAsync<JsonElement>();
            var raw = body.GetProperty("refreshToken").GetString();
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = raw })));
            Assert.Single(results.Where(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Single(results.Where(x => x.StatusCode == HttpStatusCode.Unauthorized));
            Assert.Equal(2, await context.RefreshTokens.CountAsync(x => x.UserId == user.Id));
            Assert.Equal(1, await context.RefreshTokens.CountAsync(x => x.UserId == user.Id && x.RevokedAt == null));
        }
        // Only this helper's GUID-named disposable test DB is migrated down.
        var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261016160000_SeedOrganizerRole");
        Assert.True(await context.Events.AnyAsync(x => x.Id == database.EventId));
        await context.Database.MigrateAsync();
        Assert.Empty(await context.RefreshTokens.ToListAsync());
        Assert.True(await context.Events.AnyAsync(x => x.Id == database.EventId));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Concurrent_capacity_update_and_reservation_never_oversell()
    {
        await using var database = await SqlServerProcedureTestDatabase.CreateAsync(Server());
        await SeedManagerAsync(database);
        for (var run = 0; run < 8; run++)
        {
            var id = await database.AddInventoryAsync("capacity race", 2);
            await using var reserveContext = database.CreateContext();
            await using var editContext = database.CreateContext();
            var service = new TicketTypeService(editContext, new OrganizationAuthorizer(editContext), new TestTimeProvider(database.Now));
            var editTask = service.UpdateAsync(id, database.UserId, new SaveTicketTypeRequest
            {
                Name = $"edited-{run}", Price = 500000, Currency = "VND", TotalQuantity = 1, MaxQuantityPerOrder = 1,
                SaleStartsAt = database.Now.AddDays(-1), SaleEndsAt = database.Now.AddDays(1)
            });
            var reserveTask = database.CreateReservationService(reserveContext).CreateAsync(database.UserId, $"race-{run}", new CreateReservationRequest
            { EventId = database.EventId, Items = [new() { TicketTypeId = id, Quantity = 2 }] });
            await Task.WhenAll(editTask, reserveTask);
            var edit = await editTask;
            var reservation = await reserveTask;
            Assert.Contains(edit.Error, new[] { TicketTypeError.None, TicketTypeError.InventoryCommitted });
            Assert.Contains(reservation.Error, new[] { ReservationError.None, ReservationError.QuantityLimitExceeded, ReservationError.InventoryInsufficient });
            await using var assertContext = database.CreateContext();
            var inventory = await assertContext.TicketTypes.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.True(inventory.ReservedQuantity + (long)inventory.SoldQuantity <= inventory.TotalQuantity);
            Assert.False(edit.Error == TicketTypeError.None && reservation.Error == ReservationError.None);
        }
    }

    [Fact]
    public async Task Disabling_and_reserving_serialize_without_cancelling_existing_holds()
    {
        await using var database = await SqlServerProcedureTestDatabase.CreateAsync(Server());
        await SeedManagerAsync(database);
        var id = await database.AddInventoryAsync("status race", 3);
        await using var editContext = database.CreateContext();
        await using var reserveContext = database.CreateContext();
        var service = new TicketTypeService(editContext, new OrganizationAuthorizer(editContext), new TestTimeProvider(database.Now));
        var disable = service.SetStatusAsync(id, database.UserId, false);
        var reserve = database.CreateReservationService(reserveContext).CreateAsync(database.UserId, "before-disable", new CreateReservationRequest
        { EventId = database.EventId, Items = [new() { TicketTypeId = id, Quantity = 1 }] });
        await Task.WhenAll(disable, reserve);
        Assert.Equal(TicketTypeError.None, (await disable).Error);
        Assert.Contains((await reserve).Error, new[] { ReservationError.None, ReservationError.TicketTypeNotOnSale });
        await using var subsequent = database.CreateContext();
        Assert.Equal(ReservationError.TicketTypeNotOnSale, (await database.CreateReservationService(subsequent).CreateAsync(database.UserId, "after-disable", new CreateReservationRequest
        { EventId = database.EventId, Items = [new() { TicketTypeId = id, Quantity = 1 }] })).Error);
        var inventory = await subsequent.TicketTypes.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal((await reserve).Error == ReservationError.None ? 1 : 0, inventory.ReservedQuantity);
    }

    private static async Task SeedManagerAsync(SqlServerProcedureTestDatabase database)
    {
        await using var context = database.CreateContext();
        var evt = await context.Events.SingleAsync(x => x.Id == database.EventId);
        context.OrganizationMembers.Add(new OrganizationMember { OrganizationId = evt.OrganizationId, UserId = database.UserId, Role = OrganizationMemberRole.Owner });
        await context.SaveChangesAsync();
    }
}
