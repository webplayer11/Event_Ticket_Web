using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventGO.Tests;

/// <summary>
/// API EventCategory và kiểm tra JSON đồng nhất với API hiện có:
/// thành công trả DTO trần (camelCase), lỗi nghiệp vụ { message, errors? },
/// lỗi validation là ValidationProblemDetails (application/problem+json).
/// </summary>
public sealed class EventCategoryApiTests : IAsyncLifetime
{
    private const string ValidPassword = "Valid!Pass123";
    private const string MusicCategoryId = "0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01";

    private static readonly string[] CategoryKeys = ["id", "name", "slug", "description", "isActive"];
    private static readonly string[] ProblemKeys = ["type", "title", "status", "errors", "traceId"];

    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.InitializeDatabaseAsync();

        // Seed sự kiện trực tiếp cần tắt kiểm tra khóa ngoại của SQLite.
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ───────────────────────── GET ─────────────────────────

    [Fact]
    public async Task Get_is_public_and_returns_bare_array_of_active_categories()
    {
        var response = await _client.GetAsync("/api/event-categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(5, body.GetArrayLength());
        foreach (var category in body.EnumerateArray())
        {
            AssertKeys(category, CategoryKeys);
            Assert.True(category.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Include_inactive_is_admin_only()
    {
        var anonymous = await _client.GetAsync("/api/event-categories?includeInactive=true");
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        AssertKeys(await ReadJsonAsync(anonymous), "message");

        var customerToken = await LoginNewUserAsync("customer");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, "/api/event-categories?includeInactive=true", customerToken)).StatusCode);

        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);
        var inactiveId = (await ReadJsonAsync(await CreateAsync(adminToken, new { name = "Tạm ngưng" })))
            .GetProperty("id").GetGuid();
        await SetStatusAsync(adminToken, inactiveId, false);

        var all = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/event-categories?includeInactive=true", adminToken));
        Assert.Contains(all.EnumerateArray(), x => x.GetProperty("id").GetGuid() == inactiveId
            && !x.GetProperty("isActive").GetBoolean());

        var publicList = await ReadJsonAsync(await _client.GetAsync("/api/event-categories"));
        Assert.DoesNotContain(publicList.EnumerateArray(), x => x.GetProperty("id").GetGuid() == inactiveId);
    }

    // ───────────────────────── POST ─────────────────────────

    [Fact]
    public async Task Create_requires_platform_admin()
    {
        var anonymous = await _client.PostAsJsonAsync("/api/event-categories", new { name = "Ẩn danh" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, anonymous.Content.Headers.ContentLength ?? 0);

        var customerToken = await LoginNewUserAsync("customer");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateAsync(customerToken, new { name = "Khách tạo" })).StatusCode);
    }

    [Fact]
    public async Task Create_generates_or_validates_slug_and_rejects_duplicates()
    {
        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);

        var created = await CreateAsync(adminToken, new
        {
            name = "  Âm nhạc dân tộc ",
            description = "Nhạc cụ truyền thống"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJsonAsync(created);
        AssertKeys(body, CategoryKeys);
        Assert.Equal("Âm nhạc dân tộc", body.GetProperty("name").GetString());
        Assert.Equal("am-nhac-dan-toc", body.GetProperty("slug").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        // Trùng slug (cả với category seed sẵn) → 409 { message }.
        var duplicate = await CreateAsync(adminToken, new { name = "Âm nhạc 2", slug = "am-nhac" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertKeys(await ReadJsonAsync(duplicate), "message");

        // Slug sai định dạng → validation tự động (problem+json, khóa PascalCase).
        var badSlug = await CreateAsync(adminToken, new { name = "Hợp lệ", slug = "Âm Nhạc" });
        await AssertValidationProblemAsync(badSlug, "Slug");

        var shortName = await CreateAsync(adminToken, new { name = "A" });
        await AssertValidationProblemAsync(shortName, "Name");

        // Tên không sinh được slug → lỗi nghiệp vụ { message, errors }.
        var noSlug = await CreateAsync(adminToken, new { name = "!!!" });
        Assert.Equal(HttpStatusCode.BadRequest, noSlug.StatusCode);
        Assert.Equal("application/json", noSlug.Content.Headers.ContentType?.MediaType);
        AssertKeys(await ReadJsonAsync(noSlug), "message", "errors");
    }

    // ───────────────────────── PUT ─────────────────────────

    [Fact]
    public async Task Update_changes_name_slug_description_and_keeps_slug_unique()
    {
        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);
        var id = (await ReadJsonAsync(await CreateAsync(adminToken, new { name = "Ẩm thực" })))
            .GetProperty("id").GetGuid();

        var updated = await SendAsync(HttpMethod.Put, $"/api/event-categories/{id}", adminToken, new
        {
            name = "Ẩm thực & Lễ hội",
            slug = "am-thuc-le-hoi",
            description = "Hội chợ, lễ hội ẩm thực"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await ReadJsonAsync(updated);
        AssertKeys(body, CategoryKeys);
        Assert.Equal("am-thuc-le-hoi", body.GetProperty("slug").GetString());
        Assert.Equal("Hội chợ, lễ hội ẩm thực", body.GetProperty("description").GetString());

        // Giữ nguyên slug của chính mình không bị coi là trùng.
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Put, $"/api/event-categories/{id}", adminToken,
                new { name = "Ẩm thực", slug = "am-thuc-le-hoi" })).StatusCode);

        var duplicate = await SendAsync(HttpMethod.Put, $"/api/event-categories/{id}", adminToken,
            new { name = "Ẩm thực", slug = "the-thao" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertKeys(await ReadJsonAsync(duplicate), "message");

        var missing = await SendAsync(HttpMethod.Put, $"/api/event-categories/{Guid.NewGuid()}", adminToken,
            new { name = "Không có", slug = "khong-co" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        AssertKeys(await ReadJsonAsync(missing), "message");

        await AssertValidationProblemAsync(
            await SendAsync(HttpMethod.Put, $"/api/event-categories/{id}", adminToken, new { name = "Thiếu slug" }),
            "Slug");
    }

    // ───────────────────────── PATCH status ─────────────────────────

    [Fact]
    public async Task Status_cannot_disable_category_with_published_ongoing_events()
    {
        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);
        var categoryId = Guid.Parse(MusicCategoryId);

        await SeedEventAsync(categoryId, EventStatus.Published, endsInDays: 3);
        // Sự kiện đã kết thúc và sự kiện chờ duyệt không chặn việc tắt.
        await SeedEventAsync(categoryId, EventStatus.Published, endsInDays: -1);
        await SeedEventAsync(categoryId, EventStatus.PendingApproval, endsInDays: 5);

        var blocked = await SetStatusAsync(adminToken, categoryId, false);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedBody = await ReadJsonAsync(blocked);
        AssertKeys(blockedBody, "message", "errors");
        Assert.Contains("1 sự kiện", blockedBody.GetProperty("errors")[0].GetString());

        // Category không có sự kiện đang bán: tắt được, gửi lại cùng trạng thái vẫn 200.
        var sportsId = Guid.Parse("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b02");
        var disabled = await SetStatusAsync(adminToken, sportsId, false);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var disabledBody = await ReadJsonAsync(disabled);
        AssertKeys(disabledBody, CategoryKeys);
        Assert.False(disabledBody.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await SetStatusAsync(adminToken, sportsId, false)).StatusCode);

        // Không tạo được sự kiện mới với category đã tắt.
        var userToken = await LoginNewUserAsync("creator");
        var eventWithDisabledCategory = await SendAsync(HttpMethod.Post, "/api/events", userToken,
            EventRequest(sportsId));
        Assert.Equal(HttpStatusCode.BadRequest, eventWithDisabledCategory.StatusCode);
        AssertKeys(await ReadJsonAsync(eventWithDisabledCategory), "message");

        Assert.True((await ReadJsonAsync(await SetStatusAsync(adminToken, sportsId, true)))
            .GetProperty("isActive").GetBoolean());

        var missing = await SetStatusAsync(adminToken, Guid.NewGuid(), false);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        AssertKeys(await ReadJsonAsync(missing), "message");

        // Bắt buộc gửi rõ isActive.
        await AssertValidationProblemAsync(
            await SendAsync(HttpMethod.Patch, $"/api/event-categories/{sportsId}/status", adminToken, new { }),
            "IsActive");
    }

    // ───────────────────── Đồng nhất với API hiện có ─────────────────────

    [Fact]
    public async Task Error_and_validation_shapes_match_existing_apis()
    {
        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);

        // 404 { message } giống PlatformAdminUsers.
        var existing404 = await SendAsync(HttpMethod.Get,
            $"/api/platform-admin/users?email=missing-{Guid.NewGuid():N}@example.com", adminToken);
        var category404 = await SetStatusAsync(adminToken, Guid.NewGuid(), true);
        Assert.Equal(existing404.StatusCode, category404.StatusCode);
        Assert.Equal(KeysOf(await ReadJsonAsync(existing404)), KeysOf(await ReadJsonAsync(category404)));

        // Validation: cùng dạng ValidationProblemDetails với register.
        var existing400 = await _client.PostAsJsonAsync("/api/auth/register", new { email = "x" });
        var category400 = await CreateAsync(adminToken, new { name = "" });
        Assert.Equal(existing400.Content.Headers.ContentType?.MediaType, category400.Content.Headers.ContentType?.MediaType);
        Assert.Equal(KeysOf(await ReadJsonAsync(existing400)), KeysOf(await ReadJsonAsync(category400)));

        // Lỗi nhiều lý do { message, errors[] } giống register thất bại (mật khẩu yếu).
        var existingMulti = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Weak Password",
            email = $"weak-{Guid.NewGuid():N}@example.com",
            password = "weakpassword",
            confirmPassword = "weakpassword"
        });
        var eventMulti = await SendAsync(HttpMethod.Post, "/api/events", adminToken,
            EventRequest(Guid.Parse(MusicCategoryId), startsInDays: -1));
        Assert.Equal(HttpStatusCode.BadRequest, existingMulti.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, eventMulti.StatusCode);
        var eventMultiBody = await ReadJsonAsync(eventMulti);
        Assert.Equal(KeysOf(await ReadJsonAsync(existingMulti)), KeysOf(eventMultiBody));
        Assert.Equal(JsonValueKind.String, eventMultiBody.GetProperty("errors")[0].ValueKind);
    }

    [Fact]
    public async Task Admin_event_list_rejects_unknown_status_instead_of_silently_defaulting()
    {
        var adminToken = await LoginNewUserAsync("admin", SystemRoles.PlatformAdmin);

        var invalid = await SendAsync(HttpMethod.Get, "/api/platform-admin/events?status=bogus", adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        AssertKeys(await ReadJsonAsync(invalid), "message", "errors");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(HttpMethod.Get, "/api/platform-admin/events?status=4", adminToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, "/api/platform-admin/events?status=published", adminToken)).StatusCode);

        var count = await ReadJsonAsync(
            await SendAsync(HttpMethod.Get, "/api/platform-admin/events/pending-count", adminToken));
        AssertKeys(count, "count");
    }

    [Fact]
    public async Task Swagger_describes_actual_category_responses()
    {
        // Swagger chỉ bật ở Development (Program.cs). Tắt worker quét giữ chỗ
        // để không phụ thuộc user-secrets của máy chạy test.
        await using var devFactory = _factory.WithWebHostBuilder(builder =>
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

        var swagger = await ReadJsonAsync(await client.GetAsync("/swagger/v1/swagger.json"));
        var paths = swagger.GetProperty("paths");
        var schemas = swagger.GetProperty("components").GetProperty("schemas");

        var list = paths.GetProperty("/api/event-categories").GetProperty("get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", list.GetProperty("type").GetString());
        Assert.EndsWith("/EventCategoryResponse", list.GetProperty("items").GetProperty("$ref").GetString());

        var create = paths.GetProperty("/api/event-categories").GetProperty("post").GetProperty("responses");
        Assert.EndsWith("/EventCategoryResponse", SchemaRef(create, "201", "application/json"));
        Assert.EndsWith("/ValidationProblemDetails", SchemaRef(create, "400", "application/problem+json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(create, "400", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(create, "409", "application/json"));
        // 401/403 do xác thực/phân quyền thực tế có body rỗng.
        Assert.False(create.GetProperty("401").TryGetProperty("content", out _));
        Assert.False(create.GetProperty("403").TryGetProperty("content", out _));

        // API Event dùng cùng chuẩn: 400 có cả { message, errors } và ValidationProblemDetails.
        var createEvent = paths.GetProperty("/api/events").GetProperty("post").GetProperty("responses");
        Assert.EndsWith("/ManagedEventResponse", SchemaRef(createEvent, "201", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(createEvent, "400", "application/json"));
        Assert.EndsWith("/ValidationProblemDetails", SchemaRef(createEvent, "400", "application/problem+json"));

        var status = paths.GetProperty("/api/event-categories/{id}/status").GetProperty("patch").GetProperty("responses");
        Assert.EndsWith("/EventCategoryResponse", SchemaRef(status, "200", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(status, "404", "application/json"));
        Assert.EndsWith("/MessageResponse", SchemaRef(status, "409", "application/json"));
        Assert.True(paths.GetProperty("/api/event-categories/{id}").TryGetProperty("put", out _));

        // Schema khớp JSON thực tế: camelCase.
        Assert.Equal(
            CategoryKeys.Order(),
            schemas.GetProperty("EventCategoryResponse").GetProperty("properties")
                .EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(
            new[] { "errors", "message" },
            schemas.GetProperty("MessageResponse").GetProperty("properties")
                .EnumerateObject().Select(x => x.Name).Order());
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static string? SchemaRef(JsonElement responses, string status, string mediaType) =>
        responses.GetProperty(status).GetProperty("content").GetProperty(mediaType)
            .GetProperty("schema").GetProperty("$ref").GetString();

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        AssertKeys(body, ProblemKeys);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _), $"Thiếu lỗi cho {field}");
    }

    private static void AssertKeys(JsonElement element, params string[] expected) =>
        Assert.Equal(expected.Order(), KeysOf(element));

    private static IEnumerable<string> KeysOf(JsonElement element) =>
        element.EnumerateObject().Select(x => x.Name).Order().ToList();

    private Task<HttpResponseMessage> CreateAsync(string token, object body) =>
        SendAsync(HttpMethod.Post, "/api/event-categories", token, body);

    private Task<HttpResponseMessage> SetStatusAsync(string token, Guid id, bool isActive) =>
        SendAsync(HttpMethod.Patch, $"/api/event-categories/{id}/status", token, new { isActive });

    private async Task SeedEventAsync(Guid categoryId, EventStatus status, int endsInDays)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var endsAt = DateTimeOffset.UtcNow.AddDays(endsInDays);
        dbContext.Events.Add(new Event
        {
            OrganizationId = Guid.NewGuid(),
            CategoryId = categoryId,
            CreatedByUserId = Guid.NewGuid(),
            Title = "Seed",
            Slug = $"seed-{Guid.NewGuid():N}",
            Description = "Seed",
            VenueName = "Venue",
            Address = "Address",
            City = "HCM",
            StartsAt = endsAt.AddHours(-3),
            EndsAt = endsAt,
            Status = status,
            RowVersion = [1]
        });
        await dbContext.SaveChangesAsync();
    }

    private static object EventRequest(Guid categoryId, int startsInDays = 10)
    {
        var start = DateTimeOffset.UtcNow.AddDays(startsInDays);
        return new
        {
            title = "Sự kiện thử",
            description = "Mô tả",
            categoryId,
            venueName = "Nhà hát",
            address = "1 Tràng Tiền",
            city = "Hà Nội",
            startsAt = start,
            endsAt = start.AddHours(3),
            ticketTypes = new[]
            {
                new
                {
                    name = "Thường",
                    price = 100_000m,
                    totalQuantity = 10,
                    maxQuantityPerOrder = 2,
                    saleStartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                    saleEndsAt = start
                }
            }
        };
    }

    private async Task<string> LoginNewUserAsync(string prefix, string role = SystemRoles.Customer)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        await _factory.CreateUserAsync(email, ValidPassword, role: role);
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });
        login.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
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
