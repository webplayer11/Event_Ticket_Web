using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EventGO.Tests;

/// <summary>
/// Luồng Draft → PendingApproval → Approved → Published / Rejected / Cancelled,
/// phạm vi tổ chức và tìm kiếm công khai. Đồng thời kiểm tra JSON giữ đúng
/// chuẩn hiện có: DTO không bọc, mảng thuần, lỗi { message, errors? },
/// validation là ValidationProblemDetails.
/// </summary>
public sealed class EventLifecycleApiTests : IAsyncLifetime
{
    private const string ValidPassword = "Valid!Pass123";
    private static readonly Guid MusicCategoryId = Guid.Parse("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01");

    private static readonly string[] ProblemKeys = ["errors", "status", "title", "traceId", "type"];

    private readonly AuthApiFactory _baseFactory = new();
    private readonly CapturingEventApprovalNotifier _notifier = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEventApprovalNotifier>();
                services.AddSingleton<IEventApprovalNotifier>(_notifier);
            }));
        _client = _factory.CreateClient();
        await _baseFactory.InitializeDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }

    [Fact]
    public async Task Full_lifecycle_draft_submit_reject_resubmit_approve_publish_cancel()
    {
        var org = await CreateOrganizationAsync();
        var (adminToken, _) = await CreateUserAsync("admin", SystemRoles.PlatformAdmin);

        // Tạo nháp chưa có loại vé: được, không báo Admin.
        var created = await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events",
            org.OwnerToken, EventBody("Đêm nhạc mùa thu", withTickets: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/json", created.Content.Headers.ContentType?.MediaType);
        var draft = await ReadJsonAsync(created);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal(org.Id, draft.GetProperty("organizationId").GetGuid());
        Assert.Equal(0, draft.GetProperty("ticketTypes").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("publishedAt").ValueKind);
        Assert.Empty(_notifier.Notifications);
        var eventId = draft.GetProperty("id").GetGuid();
        var slug = draft.GetProperty("slug").GetString()!;

        // Nháp: người ngoài không thấy, Owner xem trước được.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"/api/events/{slug}", org.OwnerToken)).StatusCode);

        // Gửi duyệt khi chưa có loại vé → 400 { message, errors }.
        var noTickets = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", org.OwnerToken);
        await AssertMessageAsync(noTickets, HttpStatusCode.BadRequest, withErrors: true);

        // Chưa duyệt thì không xuất bản được.
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/publish", org.OwnerToken),
            HttpStatusCode.Conflict);

        // Manager sửa nháp, thêm loại vé.
        var updated = await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.ManagerToken,
            EventBody("Đêm nhạc mùa thu 2026"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await ReadJsonAsync(updated);
        Assert.Equal("Draft", updatedBody.GetProperty("status").GetString());
        Assert.Equal(2, updatedBody.GetProperty("ticketTypes").GetArrayLength());

        // ticketTypes = null: giữ nguyên loại vé.
        var keepTickets = await ReadJsonAsync(await SendAsync(HttpMethod.Put, $"/api/events/{eventId}",
            org.OwnerToken, EventBody("Đêm nhạc mùa thu 2026", withTickets: false)));
        Assert.Equal(2, keepTickets.GetProperty("ticketTypes").GetArrayLength());

        var submitted = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", org.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal("PendingApproval", (await ReadJsonAsync(submitted)).GetProperty("status").GetString());
        Assert.False(Assert.Single(_notifier.Notifications).IsResubmission);

        // Đang chờ duyệt: không sửa, không hủy, không gửi lại.
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.OwnerToken, EventBody("Đổi")),
            HttpStatusCode.Conflict);
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/cancel", org.OwnerToken, new { }),
            HttpStatusCode.Conflict);
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", org.OwnerToken),
            HttpStatusCode.Conflict);

        // Admin thấy trong hàng chờ ở route mới.
        var pending = await ReadJsonAsync(await SendAsync(HttpMethod.Get, "/api/admin/events", adminToken));
        Assert.Equal(eventId, Assert.Single(pending.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, (await ReadJsonAsync(await SendAsync(HttpMethod.Get, "/api/admin/events/pending-count", adminToken)))
            .GetProperty("count").GetInt32());

        // Tổ chức không gọi được API duyệt.
        var forbidden = await SendAsync(HttpMethod.Post, $"/api/admin/events/{eventId}/approve", org.OwnerToken, new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, (await forbidden.Content.ReadAsByteArrayAsync()).Length);

        // Từ chối → sửa → gửi lại (báo Admin là gửi lại).
        var rejected = await SendAsync(HttpMethod.Post, $"/api/admin/events/{eventId}/reject", adminToken,
            new { note = "Bổ sung địa chỉ chi tiết." });
        Assert.Equal("Rejected", (await ReadJsonAsync(rejected)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.OwnerToken,
            EventBody("Đêm nhạc mùa thu 2026", address: "1 Tràng Tiền, Hoàn Kiếm"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", org.OwnerToken)).StatusCode);
        Assert.True(_notifier.Notifications.Last().IsResubmission);

        var approved = await SendAsync(HttpMethod.Post, $"/api/admin/events/{eventId}/approve", adminToken, new { note = "OK" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedBody = await ReadJsonAsync(approved);
        Assert.Equal("Approved", approvedBody.GetProperty("status").GetString());
        Assert.Equal("OK", approvedBody.GetProperty("lastReviewNote").GetString());

        // Đã duyệt nhưng chưa xuất bản: chưa công khai; không sửa được nữa.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{slug}")).StatusCode);
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.OwnerToken, EventBody("Đổi")),
            HttpStatusCode.Conflict);

        // Staff không xuất bản được.
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/publish", org.StaffToken),
            HttpStatusCode.Forbidden);

        var published = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/publish", org.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedBody = await ReadJsonAsync(published);
        Assert.Equal("Published", publishedBody.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, publishedBody.GetProperty("publishedAt").ValueKind);

        // Công khai theo slug và theo id, không lộ thông tin quản trị.
        var publicBySlug = await _client.GetAsync($"/api/events/{slug}");
        Assert.Equal(HttpStatusCode.OK, publicBySlug.StatusCode);
        var publicJson = await ReadJsonAsync(publicBySlug);
        Assert.Equal(PublicEventKeys.Order(), KeysOf(publicJson));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/events/{eventId}")).StatusCode);

        // Hủy (chưa có vé bán) → ẩn khỏi công khai.
        var cancelled = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/cancel", org.OwnerToken,
            new { reason = "Đổi lịch nghệ sĩ." });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await ReadJsonAsync(cancelled)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{slug}")).StatusCode);

        // Đã hủy là trạng thái cuối.
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/publish", org.OwnerToken),
            HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var history = (await dbContext.EventApprovalHistories.Where(x => x.EventId == eventId).ToListAsync())
            .OrderBy(x => x.CreatedAt)
            .Select(x => (x.FromStatus, x.ToStatus))
            .ToList();
        Assert.Equal(
            [
                (EventStatus.Draft, EventStatus.PendingApproval),
                (EventStatus.PendingApproval, EventStatus.Rejected),
                (EventStatus.Rejected, EventStatus.PendingApproval),
                (EventStatus.PendingApproval, EventStatus.Approved),
                (EventStatus.Approved, EventStatus.Published),
                (EventStatus.Published, EventStatus.Cancelled)
            ],
            history);
    }

    [Fact]
    public async Task Draft_cannot_be_approved_and_draft_can_be_cancelled()
    {
        var org = await CreateOrganizationAsync();
        var (adminToken, _) = await CreateUserAsync("admin", SystemRoles.PlatformAdmin);
        var eventId = await CreateDraftAsync(org);

        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/admin/events/{eventId}/approve", adminToken, new { }),
            HttpStatusCode.Conflict);

        // Body hủy không bắt buộc.
        var cancelled = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/cancel", org.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await ReadJsonAsync(cancelled)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_is_blocked_after_tickets_are_reserved_or_sold()
    {
        var org = await CreateOrganizationAsync();
        var eventId = await SeedEventAsync(org.Id, org.OwnerId, "Đã bán vé", EventStatus.Published, soldQuantity: 3);

        var response = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/cancel", org.OwnerToken, new { });
        await AssertMessageAsync(response, HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        Assert.Equal(EventStatus.Published, (await dbContext.Events.SingleAsync(x => x.Id == eventId)).Status);
    }

    [Fact]
    public async Task Organization_scope_is_enforced_for_create_update_and_list()
    {
        var org = await CreateOrganizationAsync();
        var other = await CreateOrganizationAsync();
        var eventId = await CreateDraftAsync(org);
        var (outsiderToken, _) = await CreateUserAsync("outsider");

        // Người ngoài: 404 (không lộ tổ chức/sự kiện), Staff: 403.
        foreach (var token in new[] { outsiderToken, other.OwnerToken })
        {
            await AssertMessageAsync(
                await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events", token, EventBody("Mạo danh")),
                HttpStatusCode.NotFound);
            await AssertMessageAsync(
                await SendAsync(HttpMethod.Get, $"/api/organizations/{org.Id}/events", token),
                HttpStatusCode.NotFound);
            await AssertMessageAsync(
                await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", token, EventBody("Mạo danh")),
                HttpStatusCode.NotFound);
            await AssertMessageAsync(
                await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", token),
                HttpStatusCode.NotFound);
        }

        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events", org.StaffToken, EventBody("Mạo danh")),
            HttpStatusCode.Forbidden);
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Get, $"/api/organizations/{org.Id}/events", org.StaffToken),
            HttpStatusCode.Forbidden);
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.StaffToken, EventBody("Mạo danh")),
            HttpStatusCode.Forbidden);

        // Không đăng nhập: 401 body rỗng.
        var anonymous = await _client.GetAsync($"/api/organizations/{org.Id}/events");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, (await anonymous.Content.ReadAsByteArrayAsync()).Length);

        // Danh sách của tổ chức: mảng thuần, chỉ sự kiện của tổ chức, lọc theo trạng thái.
        await SeedEventAsync(org.Id, org.OwnerId, "Đang bán", EventStatus.Published);
        await SeedEventAsync(other.Id, other.OwnerId, "Tổ chức khác", EventStatus.Published);

        var all = await ReadJsonAsync(await SendAsync(HttpMethod.Get, $"/api/organizations/{org.Id}/events", org.ManagerToken));
        Assert.Equal(JsonValueKind.Array, all.ValueKind);
        Assert.Equal(2, all.GetArrayLength());
        Assert.All(all.EnumerateArray(), x => Assert.Equal(org.Id, x.GetProperty("organizationId").GetGuid()));

        var drafts = await ReadJsonAsync(await SendAsync(HttpMethod.Get, $"/api/organizations/{org.Id}/events?status=draft", org.OwnerToken));
        Assert.Equal(eventId, Assert.Single(drafts.EnumerateArray()).GetProperty("id").GetGuid());

        await AssertMessageAsync(
            await SendAsync(HttpMethod.Get, $"/api/organizations/{org.Id}/events?status=Paused", org.OwnerToken),
            HttpStatusCode.BadRequest,
            withErrors: true);
    }

    [Fact]
    public async Task Create_and_update_validation_use_existing_error_shapes()
    {
        var org = await CreateOrganizationAsync();

        // DataAnnotations → ValidationProblemDetails (khóa PascalCase như API hiện có).
        var missingTitle = await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events",
            org.OwnerToken, EventBody(""));
        await AssertValidationProblemAsync(missingTitle, "Title");

        // Quy tắc nghiệp vụ → { message, errors }.
        var past = await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events",
            org.OwnerToken, EventBody("Quá khứ", startsAt: DateTimeOffset.UtcNow.AddDays(-1)));
        var errors = await AssertMessageAsync(past, HttpStatusCode.BadRequest, withErrors: true);
        Assert.Contains("Thời gian bắt đầu phải ở tương lai.", errors);

        // Thể loại không tồn tại → { message } không có errors.
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events", org.OwnerToken,
                EventBody("Thể loại lạ", categoryId: Guid.NewGuid())),
            HttpStatusCode.BadRequest);

        // Sửa thời gian làm loại vé hiện có (giữ nguyên) không còn hợp lệ.
        var eventId = await CreateDraftAsync(org);
        var shortened = EventBody("Rút ngắn", withTickets: false, endsAfterStartHours: 1,
            startsAt: DateTimeOffset.UtcNow.AddDays(1));
        await AssertMessageAsync(
            await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", org.OwnerToken, shortened),
            HttpStatusCode.BadRequest,
            withErrors: true);
    }

    [Fact]
    public async Task Public_search_filters_and_paginates_published_events_only()
    {
        var org = await CreateOrganizationAsync();
        var categories = await ReadJsonAsync(await _client.GetAsync("/api/event-categories"));
        var otherCategoryId = categories.EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .First(x => x != MusicCategoryId);

        await SeedEventAsync(org.Id, org.OwnerId, "Đêm nhạc Hà Nội", EventStatus.Published, startsInDays: 5, price: 200_000m);
        await SeedEventAsync(org.Id, org.OwnerId, "Hòa nhạc Sài Gòn", EventStatus.Published, startsInDays: 10, city: "Hồ Chí Minh", price: 800_000m);
        await SeedEventAsync(org.Id, org.OwnerId, "Hội thảo công nghệ", EventStatus.Published, startsInDays: 20, categoryId: otherCategoryId, price: 0m);
        // Không xuất hiện: chưa công khai, đã hủy, đã kết thúc.
        await SeedEventAsync(org.Id, org.OwnerId, "Nhạc nháp", EventStatus.Approved);
        await SeedEventAsync(org.Id, org.OwnerId, "Nhạc đã hủy", EventStatus.Cancelled);
        await SeedEventAsync(org.Id, org.OwnerId, "Nhạc đã diễn", EventStatus.Published, startsInDays: -3);

        var all = await _client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal("3", all.Headers.GetValues("X-Total-Count").Single());
        var allBody = await ReadJsonAsync(all);
        Assert.Equal(JsonValueKind.Array, allBody.ValueKind);
        Assert.Equal(
            ["Đêm nhạc Hà Nội", "Hòa nhạc Sài Gòn", "Hội thảo công nghệ"],
            Titles(allBody));
        var first = allBody[0];
        Assert.Equal(PublicEventKeys.Order(), KeysOf(first));
        Assert.Equal("Published", first.GetProperty("status").GetString());

        Assert.Equal(["Đêm nhạc Hà Nội", "Hòa nhạc Sài Gòn"], await SearchTitlesAsync("q=NH%E1%BA%A0C"));
        Assert.Equal(["Hội thảo công nghệ"], await SearchTitlesAsync($"categoryId={otherCategoryId}"));
        Assert.Equal(["Hòa nhạc Sài Gòn"], await SearchTitlesAsync("city=h%E1%BB%93%20ch%C3%AD%20minh"));
        Assert.Equal(["Hòa nhạc Sài Gòn", "Hội thảo công nghệ"],
            await SearchTitlesAsync($"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("O"))}"));
        Assert.Equal(["Đêm nhạc Hà Nội"],
            await SearchTitlesAsync($"to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("O"))}"));
        Assert.Equal(["Đêm nhạc Hà Nội", "Hòa nhạc Sài Gòn"], await SearchTitlesAsync("minPrice=100000"));
        Assert.Equal(["Đêm nhạc Hà Nội", "Hội thảo công nghệ"], await SearchTitlesAsync("maxPrice=500000"));
        Assert.Equal(["Hòa nhạc Sài Gòn"], await SearchTitlesAsync("minPrice=300000&maxPrice=900000"));

        var page2 = await _client.GetAsync("/api/events?page=2&pageSize=2");
        Assert.Equal("3", page2.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal(["Hội thảo công nghệ"], Titles(await ReadJsonAsync(page2)));

        // Tham số sai → ValidationProblemDetails.
        await AssertValidationProblemAsync(await _client.GetAsync("/api/events?pageSize=0"), "PageSize");
        await AssertValidationProblemAsync(await _client.GetAsync("/api/events?minPrice=10&maxPrice=5"), "MaxPrice");
    }

    [Fact]
    public async Task Swagger_describes_new_event_routes_with_existing_shapes()
    {
        await using var devFactory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Reservation:SweepInterval"] = "00:00:00",
                    ["Bootstrap:PlatformAdminEmail"] = ""
                }));
        });
        using var client = devFactory.CreateClient();
        var paths = (await ReadJsonAsync(await client.GetAsync("/swagger/v1/swagger.json"))).GetProperty("paths");

        var search = paths.GetProperty("/api/events").GetProperty("get").GetProperty("responses");
        var searchItems = search.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", searchItems.GetProperty("type").GetString());
        Assert.EndsWith("/EventResponse", searchItems.GetProperty("items").GetProperty("$ref").GetString());
        Assert.EndsWith("/ValidationProblemDetails", SchemaRef(search, "400", "application/problem+json"));

        Assert.EndsWith("/EventResponse", SchemaRef(
            paths.GetProperty("/api/events/{slug}").GetProperty("get").GetProperty("responses"), "200", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(
            paths.GetProperty("/api/events/{slug}").GetProperty("get").GetProperty("responses"), "404", "application/json"));

        var createDraft = paths.GetProperty("/api/organizations/{organizationId}/events").GetProperty("post").GetProperty("responses");
        Assert.EndsWith("/ManagedEventResponse", SchemaRef(createDraft, "201", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(createDraft, "400", "application/json"));
        Assert.EndsWith("/ValidationProblemDetails", SchemaRef(createDraft, "400", "application/problem+json"));
        Assert.False(createDraft.GetProperty("401").TryGetProperty("content", out _));

        foreach (var path in new[]
        {
            "/api/events/{eventId}/submit",
            "/api/events/{eventId}/publish",
            "/api/events/{eventId}/cancel",
            "/api/admin/events/{eventId}/approve",
            "/api/admin/events/{eventId}/reject"
        })
        {
            var responses = paths.GetProperty(path).GetProperty("post").GetProperty("responses");
            Assert.EndsWith("/ManagedEventResponse", SchemaRef(responses, "200", "application/json"));
            Assert.EndsWith("/MessageResponse", SchemaRef(responses, "409", "application/json"));
        }

        var list = paths.GetProperty("/api/organizations/{organizationId}/events").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.EndsWith("/ManagedEventResponse", list.GetProperty("items").GetProperty("$ref").GetString());
        Assert.True(paths.TryGetProperty("/api/admin/events", out _));
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static readonly string[] PublicEventKeys =
    [
        "id", "title", "slug", "description", "categoryId", "categoryName", "organizationId",
        "organizationName", "venueName", "address", "city", "startsAt", "endsAt", "status",
        "createdAt", "publishedAt", "ticketTypes"
    ];

    private sealed record TestOrganization(
        Guid Id,
        Guid OwnerId,
        string OwnerToken,
        string ManagerToken,
        string StaffToken);

    private async Task<TestOrganization> CreateOrganizationAsync()
    {
        var (ownerToken, ownerEmail) = await CreateUserAsync("owner", SystemRoles.Organizer);
        var organizationId = (await ReadJsonAsync(await SendAsync(HttpMethod.Post, "/api/organizations", ownerToken, new
        {
            name = $"Công ty {Guid.NewGuid():N}"[..20],
            contactEmail = "company@example.com",
            contactPhone = "0900000000",
            address = "HCM"
        }))).GetProperty("id").GetGuid();

        var (managerToken, managerEmail) = await CreateUserAsync("manager");
        var (staffToken, staffEmail) = await CreateUserAsync("staff");
        foreach (var (email, role) in new[] { (managerEmail, 2), (staffEmail, 3) })
        {
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(HttpMethod.Post,
                $"/api/organizations/{organizationId}/members", ownerToken, new { email, role })).StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var ownerId = await dbContext.Users.Where(x => x.Email == ownerEmail).Select(x => x.Id).SingleAsync();

        return new(organizationId, ownerId, ownerToken, managerToken, staffToken);
    }

    private async Task<Guid> CreateDraftAsync(TestOrganization org)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/organizations/{org.Id}/events", org.OwnerToken, EventBody("Nháp"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> SeedEventAsync(
        Guid organizationId,
        Guid createdByUserId,
        string title,
        EventStatus status,
        int startsInDays = 10,
        string city = "Hà Nội",
        Guid? categoryId = null,
        decimal price = 200_000m,
        int soldQuantity = 0)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var startsAt = DateTimeOffset.UtcNow.AddDays(startsInDays);
        var evt = new Event
        {
            OrganizationId = organizationId,
            CategoryId = categoryId ?? MusicCategoryId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Slug = $"seed-{Guid.NewGuid():N}",
            Description = "Mô tả",
            VenueName = "Nhà hát",
            Address = "1 Tràng Tiền",
            City = city,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(3),
            Status = status,
            PublishedAt = status == EventStatus.Published ? DateTimeOffset.UtcNow : null,
            RowVersion = [1]
        };
        dbContext.Events.Add(evt);
        dbContext.TicketTypes.Add(new TicketType
        {
            EventId = evt.Id,
            Name = "Thường",
            Price = price,
            TotalQuantity = 100,
            SoldQuantity = soldQuantity,
            MaxQuantityPerOrder = 5,
            SaleStartsAt = DateTimeOffset.UtcNow.AddDays(-30),
            SaleEndsAt = startsAt,
            RowVersion = [1]
        });
        await dbContext.SaveChangesAsync();
        return evt.Id;
    }

    /// <summary>withTickets = false gửi "ticketTypes": null (giữ nguyên / chưa có loại vé).</summary>
    private static object EventBody(
        string title,
        bool withTickets = true,
        Guid? categoryId = null,
        DateTimeOffset? startsAt = null,
        string address = "1 Tràng Tiền",
        int endsAfterStartHours = 4)
    {
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(10);
        var end = start.AddHours(endsAfterStartHours);
        var saleStart = DateTimeOffset.UtcNow.AddHours(-1);
        // Ngừng bán đúng lúc sự kiện kết thúc mặc định (10 ngày + 4 giờ).
        var saleEnd = DateTimeOffset.UtcNow.AddDays(10).AddHours(3);

        return new
        {
            title,
            description = "<p>Mô tả sự kiện</p>",
            categoryId = categoryId ?? MusicCategoryId,
            venueName = "Nhà hát Lớn",
            address,
            city = "Hà Nội",
            startsAt = start,
            endsAt = end,
            ticketTypes = withTickets
                ? new object[]
                {
                    new { name = "Thường", price = 200_000m, totalQuantity = 100, maxQuantityPerOrder = 5, saleStartsAt = saleStart, saleEndsAt = saleEnd },
                    new { name = "VIP", price = 500_000m, totalQuantity = 20, maxQuantityPerOrder = 2, saleStartsAt = saleStart, saleEndsAt = saleEnd }
                }
                : null
        };
    }

    private async Task<List<string>> SearchTitlesAsync(string query)
    {
        var response = await _client.GetAsync($"/api/events?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Titles(await ReadJsonAsync(response));
    }

    private static List<string> Titles(JsonElement array) =>
        array.EnumerateArray().Select(x => x.GetProperty("title").GetString()!).ToList();

    /// <summary>Body lỗi nghiệp vụ { message } hoặc { message, errors }; trả về errors.</summary>
    private static async Task<List<string>> AssertMessageAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        bool withErrors = false)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            (withErrors ? new[] { "errors", "message" } : ["message"]).Order(),
            KeysOf(body));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        return withErrors
            ? body.GetProperty("errors").EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(ProblemKeys.Order(), KeysOf(body));
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _), $"Thiếu lỗi cho {field}");
    }

    private static string? SchemaRef(JsonElement responses, string status, string mediaType) =>
        responses.GetProperty(status).GetProperty("content").GetProperty(mediaType)
            .GetProperty("schema").GetProperty("$ref").GetString();

    private static List<string> KeysOf(JsonElement element) =>
        element.EnumerateObject().Select(x => x.Name).Order().ToList();

    private async Task<(string Token, string Email)> CreateUserAsync(
        string prefix,
        string role = SystemRoles.Customer)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        await _baseFactory.CreateUserAsync(email, ValidPassword, role: role);
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        login.EnsureSuccessStatusCode();
        return ((await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!, email);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
