using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EventGO.Tests;

public sealed class EventApprovalFlowTests : IAsyncLifetime
{
    private const string ValidPassword = "Valid!Pass123";
    private const string MusicCategoryId = "0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01";

    private readonly AuthApiFactory _baseFactory = new();
    private readonly CapturingEventApprovalNotifier _notifier = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        // Cùng database SQLite với AuthApiFactory, chỉ thay phần gửi email.
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
    public async Task Categories_are_public_and_seeded()
    {
        var response = await _client.GetAsync("/api/event-categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = (await ReadJsonAsync(response)).EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToList();
        Assert.Equal(5, names.Count);
        Assert.Contains("Âm nhạc", names);
    }

    [Fact]
    public async Task Customer_creates_event_in_auto_created_personal_organization_and_admins_are_notified()
    {
        var (customerToken, _) = await CreateUserAsync("creator");

        // Quy tắc Bước 6 vẫn giữ: Customer không tự tạo tổ chức được.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Post, "/api/organizations", customerToken, new
            {
                name = "Org",
                contactEmail = "org@example.com",
                contactPhone = "0900000000",
                address = "HCM"
            })).StatusCode);

        var created = await CreateEventAsync(customerToken, "Đêm nhạc Hà Nội");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJsonAsync(created);
        Assert.Equal("PendingApproval", body.GetProperty("status").GetString());
        Assert.Equal("Ban nhạc Test", body.GetProperty("organizationName").GetString());
        Assert.StartsWith("dem-nhac-ha-noi-", body.GetProperty("slug").GetString());
        Assert.Equal(2, body.GetProperty("ticketTypes").GetArrayLength());

        var notification = Assert.Single(_notifier.Notifications);
        Assert.Equal("Đêm nhạc Hà Nội", notification.Title);
        Assert.False(notification.IsResubmission);

        // Người tạo là Owner của tổ chức cá nhân.
        var organizations = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/organizations/mine", customerToken));
        var organization = Assert.Single(organizations.EnumerateArray());
        Assert.Equal("Owner", organization.GetProperty("myRole").GetString());

        // Sự kiện thứ hai dùng lại tổ chức cá nhân, không tạo thêm.
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreateEventAsync(customerToken, "Sự kiện thứ hai")).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        Assert.Equal(1, await dbContext.Organizations.CountAsync(x => x.PersonalOwnerUserId != null));

        var mine = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/events/mine", customerToken));
        Assert.Equal(2, mine.GetArrayLength());
    }

    [Fact]
    public async Task Event_is_hidden_until_admin_approves_and_review_flow_round_trips()
    {
        var (customerToken, _) = await CreateUserAsync("flow-creator");
        var (adminToken, _) = await CreateUserAsync("flow-admin", SystemRoles.PlatformAdmin);

        var eventId = (await ReadJsonAsync(await CreateEventAsync(customerToken, "Hội thảo AI")))
            .GetProperty("id").GetGuid();

        // Chưa duyệt: không công khai.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/events/{eventId}")).StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(await _client.GetAsync("/api/events"))).GetArrayLength());

        // Customer không dùng được API duyệt.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, "/api/platform-admin/events", customerToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Post, $"/api/platform-admin/events/{eventId}/approve", customerToken, new { })).StatusCode);

        // Admin thấy trong hàng chờ.
        var count = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/platform-admin/events/pending-count", adminToken));
        Assert.Equal(1, count.GetProperty("count").GetInt32());
        var pending = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/platform-admin/events", adminToken));
        var pendingEvent = Assert.Single(pending.EnumerateArray());
        Assert.EndsWith("@example.com", pendingEvent.GetProperty("createdByEmail").GetString());

        // Từ chối phải có lý do.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Post, $"/api/platform-admin/events/{eventId}/reject", adminToken, new { note = " " })).StatusCode);
        var rejected = await SendAsync(
            HttpMethod.Post,
            $"/api/platform-admin/events/{eventId}/reject",
            adminToken,
            new { note = "Thiếu thông tin địa điểm." });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Rejected", (await ReadJsonAsync(rejected)).GetProperty("status").GetString());

        // Đã từ chối thì không duyệt được nữa (phải chờ người tạo gửi lại).
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await SendAsync(HttpMethod.Post, $"/api/platform-admin/events/{eventId}/approve", adminToken, new { })).StatusCode);

        // Người tạo thấy lý do, sửa và gửi duyệt lại → Admin được báo lại.
        var mine = Assert.Single((await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/events/mine", customerToken))).EnumerateArray());
        Assert.Equal("Thiếu thông tin địa điểm.", mine.GetProperty("lastReviewNote").GetString());

        var updated = await SendAsync(
            HttpMethod.Put,
            $"/api/events/{eventId}",
            customerToken,
            EventRequest("Hội thảo AI (đã sửa)", venueName: "Nhà văn hóa Thanh Niên"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await ReadJsonAsync(updated);
        // Sửa không tự gửi lại: phải gọi submit.
        Assert.Equal("Rejected", updatedBody.GetProperty("status").GetString());
        Assert.Equal("Nhà văn hóa Thanh Niên", updatedBody.GetProperty("venueName").GetString());

        var resubmitted = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/submit", customerToken);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        Assert.Equal("PendingApproval", (await ReadJsonAsync(resubmitted)).GetProperty("status").GetString());
        Assert.True(_notifier.Notifications.Last().IsResubmission);

        // Duyệt (route alias cũ) → Approved, chưa công khai.
        var approved = await SendAsync(
            HttpMethod.Post,
            $"/api/platform-admin/events/{eventId}/approve",
            adminToken,
            new { });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", (await ReadJsonAsync(approved)).GetProperty("status").GetString());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/events/{eventId}")).StatusCode);

        // Người tạo xuất bản → công khai, không lộ thông tin người tạo.
        var published = await SendAsync(HttpMethod.Post, $"/api/events/{eventId}/publish", customerToken);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("Published", (await ReadJsonAsync(published)).GetProperty("status").GetString());

        var publicEvent = await _client.GetAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, publicEvent.StatusCode);
        var publicJson = await publicEvent.Content.ReadAsStringAsync();
        Assert.DoesNotContain("createdByEmail", publicJson);
        Assert.DoesNotContain("lastReviewNote", publicJson);
        Assert.Equal(1, (await ReadJsonAsync(await _client.GetAsync("/api/events"))).GetArrayLength());

        // Đã công khai thì không sửa tự do được nữa.
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", customerToken, EventRequest("Đổi tên"))).StatusCode);

        // Lịch sử đầy đủ: gửi → từ chối → gửi lại → duyệt → xuất bản.
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var history = (await dbContext.EventApprovalHistories
                .Where(x => x.EventId == eventId)
                .ToListAsync())
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToStatus)
            .ToList();
        Assert.Equal(
            [EventStatus.PendingApproval, EventStatus.Rejected, EventStatus.PendingApproval, EventStatus.Approved, EventStatus.Published],
            history);
    }

    [Fact]
    public async Task Invalid_events_are_rejected_with_reasons()
    {
        var (token, _) = await CreateUserAsync("invalid");

        var past = await SendAsync(HttpMethod.Post, "/api/events", token, EventRequest(
            "Quá khứ",
            startsAt: DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains(
            "Thời gian bắt đầu phải ở tương lai.",
            (await ReadJsonAsync(past)).GetProperty("errors").EnumerateArray().Select(x => x.GetString()));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Post, "/api/events", token, EventRequest(
                "Bán vé sau khi kết thúc",
                saleEndsAt: DateTimeOffset.UtcNow.AddDays(30)))).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Post, "/api/events", token, EventRequest(
                "Thể loại lạ",
                categoryId: Guid.NewGuid()))).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Post, "/api/events", token, EventRequest(
                "Không có vé",
                withoutTickets: true))).StatusCode);

        Assert.Empty(_notifier.Notifications);
    }

    [Fact]
    public async Task Only_owner_or_manager_can_create_or_edit_events_of_an_organization()
    {
        var (organizerToken, _) = await CreateUserAsync("organizer", SystemRoles.Organizer);
        var organizationId = (await ReadJsonAsync(await SendAsync(
                HttpMethod.Post,
                "/api/organizations",
                organizerToken,
                new
                {
                    name = "Công ty sự kiện",
                    contactEmail = "company@example.com",
                    contactPhone = "0900000000",
                    address = "HCM"
                })))
            .GetProperty("id").GetGuid();

        var (staffToken, staffEmail) = await CreateUserAsync("staff");
        Assert.Equal(
            HttpStatusCode.Created,
            (await SendAsync(
                HttpMethod.Post,
                $"/api/organizations/{organizationId}/members",
                organizerToken,
                new { email = staffEmail, role = 3 })).StatusCode);

        // Organizer tạo sự kiện cho công ty: vẫn phải chờ duyệt.
        var created = await SendAsync(
            HttpMethod.Post,
            "/api/events",
            organizerToken,
            EventRequest("Sự kiện công ty", organizationId: organizationId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await ReadJsonAsync(created);
        Assert.Equal(organizationId, createdBody.GetProperty("organizationId").GetGuid());
        Assert.Equal("PendingApproval", createdBody.GetProperty("status").GetString());
        var eventId = createdBody.GetProperty("id").GetGuid();

        // Staff không tạo/sửa sự kiện của tổ chức.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Post, "/api/events", staffToken,
                EventRequest("Staff tạo", organizationId: organizationId))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", staffToken,
                EventRequest("Staff sửa"))).StatusCode);

        // Người ngoài: không thấy tổ chức/sự kiện.
        var (outsiderToken, _) = await CreateUserAsync("outsider");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Post, "/api/events", outsiderToken,
                EventRequest("Mạo danh", organizationId: organizationId))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Put, $"/api/events/{eventId}", outsiderToken,
                EventRequest("Mạo danh"))).StatusCode);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static object EventRequest(
        string title,
        Guid? organizationId = null,
        Guid? categoryId = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? saleEndsAt = null,
        string venueName = "Nhà hát Lớn",
        bool withoutTickets = false)
    {
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(10);
        var end = start.AddHours(4);
        var saleStart = DateTimeOffset.UtcNow.AddHours(-1);
        var saleEnd = saleEndsAt ?? start;

        return new
        {
            organizationId,
            organizerName = "Ban nhạc Test",
            organizerInfo = "Ban tổ chức thử nghiệm",
            title,
            description = "<p>Mô tả sự kiện</p>",
            categoryId = categoryId ?? Guid.Parse(MusicCategoryId),
            venueName,
            address = "1 Tràng Tiền",
            city = "Hà Nội",
            startsAt = start,
            endsAt = end,
            ticketTypes = withoutTickets
                ? Array.Empty<object>()
                : new object[]
                {
                    new { name = "Thường", price = 200_000m, totalQuantity = 100, maxQuantityPerOrder = 5, saleStartsAt = saleStart, saleEndsAt = saleEnd },
                    new { name = "VIP", price = 500_000m, totalQuantity = 20, maxQuantityPerOrder = 2, saleStartsAt = saleStart, saleEndsAt = saleEnd }
                }
        };
    }

    private Task<HttpResponseMessage> CreateEventAsync(string token, string title) =>
        SendAsync(HttpMethod.Post, "/api/events", token, EventRequest(title));

    private async Task<(string Token, string Email)> CreateUserAsync(
        string prefix,
        string role = SystemRoles.Customer)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        await _baseFactory.CreateUserAsync(email, ValidPassword, role: role);
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });
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
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}

internal sealed class CapturingEventApprovalNotifier : IEventApprovalNotifier
{
    private readonly ConcurrentQueue<EventSubmittedNotification> _notifications = new();

    public IReadOnlyList<EventSubmittedNotification> Notifications => _notifications.ToList();

    public Task NotifyAdminsAsync(
        EventSubmittedNotification notification,
        CancellationToken cancellationToken = default)
    {
        _notifications.Enqueue(notification);
        return Task.CompletedTask;
    }
}
