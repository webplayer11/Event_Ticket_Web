using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Application.Reservations;
using EventGO.Application.TicketTypes;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using EventGO.Infrastructure.Reservations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventGO.Tests;

public sealed class TicketTypeApiTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new();
    private HttpClient client = null!;
    private Guid eventId, ownerId;
    private string ownerToken = null!, outsiderToken = null!, staffToken = null!;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public async ValueTask InitializeAsync()
    {
        client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var owner = await factory.CreateUserAsync(email, "Valid!Pass123", role: SystemRoles.Organizer);
        ownerId = owner.Id;
        ownerToken = await LoginAsync(email);
        email = $"other-{Guid.NewGuid():N}@example.com";
        await factory.CreateUserAsync(email, "Valid!Pass123", role: SystemRoles.Organizer);
        outsiderToken = await LoginAsync(email);
        email = $"staff-{Guid.NewGuid():N}@example.com";
        var staff = await factory.CreateUserAsync(email, "Valid!Pass123");
        staffToken = await LoginAsync(email);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var org = new Organization { Name = "Ticket tests" };
        var cat = new EventCategory { Name = "Music", Slug = $"music-{Guid.NewGuid():N}" };
        var evt = new Event { OrganizationId = org.Id, CategoryId = cat.Id, CreatedByUserId = owner.Id,
            Title = "Test", Slug = $"event-{Guid.NewGuid():N}", StartsAt = now.AddDays(10), EndsAt = now.AddDays(11), Status = EventStatus.Draft };
        eventId = evt.Id;
        db.AddRange(org, cat, evt,
            new OrganizationMember { OrganizationId = org.Id, UserId = owner.Id, Role = OrganizationMemberRole.Owner },
            new OrganizationMember { OrganizationId = org.Id, UserId = staff.Id, Role = OrganizationMemberRole.Staff });
        await db.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }

    [Fact]
    public async Task Crud_visibility_ownership_response_and_status_are_enforced()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", null, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", outsiderToken, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", staffToken, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/events/{Guid.NewGuid()}/ticket-types", ownerToken, Valid())).StatusCode);
        var created = await CreateAsync();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(10, created.GetProperty("availableQuantity").GetInt32());
        Assert.False(created.TryGetProperty("soldQuantity", out _));
        Assert.False(created.TryGetProperty("reservedQuantity", out _));
        Assert.False(created.TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", ownerToken, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/events/{eventId}/ticket-types")).StatusCode);
        var managerList = await SendAsync(HttpMethod.Get, $"/api/events/{eventId}/ticket-types", ownerToken);
        Assert.Single((await managerList.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        var edit = Valid(); edit.Price = 250; edit.TotalQuantity = 12;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, $"/api/ticket-types/{id}", ownerToken, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"/api/ticket-types/{id}", outsiderToken, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Patch, $"/api/ticket-types/{id}/status", outsiderToken, new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch, $"/api/ticket-types/{id}/status", ownerToken, new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Patch, $"/api/ticket-types/{id}/status", ownerToken, new { })).StatusCode);
        await PublishAsync();
        var publicList = await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/ticket-types");
        Assert.Empty(publicList.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch, $"/api/ticket-types/{id}/status", ownerToken, new { isActive = true })).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/ticket-types")).EnumerateArray());
    }

    [Theory]
    [InlineData("price")]
    [InlineData("precision")]
    [InlineData("total")]
    [InlineData("limit")]
    [InlineData("limit-over-total")]
    [InlineData("window")]
    [InlineData("event-time")]
    [InlineData("name")]
    [InlineData("currency")]
    public async Task Invalid_input_is_rejected(string condition)
    {
        var r = Valid();
        switch (condition)
        {
            case "price": r.Price = -1; break;
            case "precision": r.Price = 1.001m; break;
            case "total": r.TotalQuantity = 0; break;
            case "limit": r.MaxQuantityPerOrder = 0; break;
            case "limit-over-total": r.MaxQuantityPerOrder = 11; break;
            case "window": r.SaleEndsAt = r.SaleStartsAt; break;
            case "event-time": r.SaleEndsAt = now.AddDays(12); break;
            case "name": r.Name = " "; break;
            case "currency": r.Currency = "INVALID"; break;
        }
        var response = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", ownerToken, r);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity });
    }

    [Fact]
    public async Task Existing_inventory_and_price_snapshots_are_protected_and_disabled_types_reject_new_holds()
    {
        var id = (await CreateAsync()).GetProperty("id").GetGuid();
        await PublishAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var ticket = await db.TicketTypes.FindAsync(id);
            ticket!.SaleStartsAt = now.AddDays(-1);
            ticket.SoldQuantity = 3;
            await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var service = new ReservationService(db, new TestReservationDatabaseOperations(db), new TestTimeProvider(now), Options.Create(new ReservationOptions { Duration = TimeSpan.FromMinutes(5) }));
            var result = await service.CreateAsync(ownerId, "hold-one", new CreateReservationRequest
            { EventId = eventId, Items = [new() { TicketTypeId = id, Quantity = 2 }] });
            Assert.Equal(ReservationError.None, result.Error);
        }
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/ticket-types");
        Assert.Equal(5, list[0].GetProperty("availableQuantity").GetInt32());
        var edit = Valid(); edit.SaleStartsAt = now.AddDays(-1); edit.TotalQuantity = 4; edit.MaxQuantityPerOrder = 2;
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Put, $"/api/ticket-types/{id}", ownerToken, edit)).StatusCode);
        edit.TotalQuantity = 10; edit.Price = 101;
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Put, $"/api/ticket-types/{id}", ownerToken, edit)).StatusCode);
        await SendAsync(HttpMethod.Patch, $"/api/ticket-types/{id}/status", ownerToken, new { isActive = false });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var clock = new TestTimeProvider(now);
            var service = new ReservationService(db, new TestReservationDatabaseOperations(db), clock, Options.Create(new ReservationOptions { Duration = TimeSpan.FromMinutes(5) }));
            Assert.Equal(ReservationError.TicketTypeNotOnSale, (await service.CreateAsync(ownerId, "disabled", new CreateReservationRequest
            { EventId = eventId, Items = [new() { TicketTypeId = id, Quantity = 1 }] })).Error);
            clock.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(1, await service.ExpireDueAsync());
        }
        var managerList = await SendAsync(HttpMethod.Get, $"/api/events/{eventId}/ticket-types", ownerToken);
        Assert.Equal(7, (await managerList.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("availableQuantity").GetInt32());
    }

    [Fact]
    public async Task Free_tickets_are_allowed_and_currency_is_uniform()
    {
        var request = Valid(); request.Price = 0;
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", ownerToken, request)).StatusCode);
        request.Name = "USD"; request.Currency = "USD";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", ownerToken, request)).StatusCode);
    }

    private SaveTicketTypeRequest Valid() => new() { Name = "VIP", Description = "VIP admission", Price = 100, Currency = "VND",
        TotalQuantity = 10, MaxQuantityPerOrder = 2, SaleStartsAt = now.AddDays(1), SaleEndsAt = now.AddDays(9) };
    private async Task<JsonElement> CreateAsync()
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/ticket-types", ownerToken, Valid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task PublishAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        (await db.Events.FindAsync(eventId))!.Status = EventStatus.Published;
        await db.SaveChangesAsync();
    }
    private async Task<string> LoginAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Valid!Pass123" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }
}
