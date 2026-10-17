using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Authentication;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventGO.Tests;

public sealed class AuthorizationHardeningTests : IAsyncLifetime
{
    private const string ValidPassword = "Valid!Pass123";
    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.InitializeDatabaseAsync();

        // Seed vé cần Order/Reservation/TicketType đầy đủ; tắt kiểm tra
        // khóa ngoại để seed gọn. Unique index vẫn được SQLite kiểm tra.
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ───────────────────────── Soát vé ─────────────────────────

    [Fact]
    public async Task Check_in_requires_active_assignment_for_the_same_event()
    {
        var org = await CreateOrganizationWithStaffAsync();
        var eventA = await SeedEventAsync(org.OrganizationId, org.OwnerId);
        var eventB = await SeedEventAsync(org.OrganizationId, org.OwnerId);
        const string qrToken = "qr-token-event-a";
        await SeedTicketAsync(eventA, org.OwnerId, qrToken);

        // Chưa được phân công → 403, kể cả là thành viên tổ chức.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(org.StaffToken, eventA, qrToken)).StatusCode);

        // Staff không được tự phân công.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await AssignStaffAsync(org.StaffToken, eventA, org.StaffMemberId)).StatusCode);

        var assignment = await AssignStaffAsync(org.OwnerToken, eventA, org.StaffMemberId);
        Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        var assignmentId = (await ReadJsonAsync(assignment)).GetProperty("id").GetGuid();

        var valid = await ValidateAsync(org.StaffToken, eventA, qrToken);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var validBody = await ReadJsonAsync(valid);
        Assert.Equal("cu***@example.com", validBody.GetProperty("customerEmail").GetString());
        Assert.False(validBody.GetProperty("isCheckedIn").GetBoolean());

        // Vé của sự kiện A mang sang sự kiện B: phải có phân công ở B,
        // và kể cả khi có thì vé vẫn "không tồn tại" ở B.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(org.StaffToken, eventB, qrToken)).StatusCode);
        await AssignStaffAsync(org.OwnerToken, eventB, org.StaffMemberId);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await ValidateAsync(org.StaffToken, eventB, qrToken)).StatusCode);

        // Check-in một lần duy nhất.
        Assert.Equal(
            HttpStatusCode.OK,
            (await CheckInAsync(org.StaffToken, eventA, qrToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await CheckInAsync(org.StaffToken, eventA, qrToken)).StatusCode);

        // Thu hồi phân công → mất quyền soát vé ngay.
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(
                HttpMethod.Delete,
                $"/api/events/{eventA}/staff/{assignmentId}",
                org.OwnerToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(org.StaffToken, eventA, qrToken)).StatusCode);

        // Người ngoài tổ chức không thấy sự kiện.
        var outsiderToken = await CreateUserAndLoginAsync("outsider");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/api/events/{eventA}/staff", outsiderToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(outsiderToken, eventA, qrToken)).StatusCode);
    }

    [Fact]
    public async Task Tickets_of_refunded_or_cancelled_orders_or_cancelled_events_cannot_check_in()
    {
        var org = await CreateOrganizationWithStaffAsync();
        var liveEvent = await SeedEventAsync(org.OrganizationId, org.OwnerId);
        var cancelledEvent = await SeedEventAsync(
            org.OrganizationId,
            org.OwnerId,
            EventStatus.Cancelled);
        await AssignStaffAsync(org.OwnerToken, liveEvent, org.StaffMemberId);
        await AssignStaffAsync(org.OwnerToken, cancelledEvent, org.StaffMemberId);

        await SeedTicketAsync(liveEvent, org.OwnerId, "qr-refunded", OrderStatus.Refunded);
        await SeedTicketAsync(liveEvent, org.OwnerId, "qr-cancelled", OrderStatus.Cancelled);
        await SeedTicketAsync(cancelledEvent, org.OwnerId, "qr-event-cancelled");

        foreach (var (eventId, qrToken) in new[]
        {
            (liveEvent, "qr-refunded"),
            (liveEvent, "qr-cancelled"),
            (cancelledEvent, "qr-event-cancelled")
        })
        {
            var validate = await ValidateAsync(org.StaffToken, eventId, qrToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, validate.StatusCode);
            Assert.Equal(
                "TICKET_NOT_VALID",
                (await ReadJsonAsync(validate)).GetProperty("error").GetProperty("code").GetString());

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                (await CheckInAsync(org.StaffToken, eventId, qrToken)).StatusCode);
        }

        // Không vé nào bị đánh dấu đã dùng.
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        Assert.Equal(0, await dbContext.CheckIns.CountAsync());
        Assert.False(await dbContext.Tickets.AnyAsync(x => x.Status == TicketStatus.Used));
    }

    [Fact]
    public async Task Endpoints_require_authentication_by_default()
    {
        // Không có endpoint nào khớp: fallback policy vẫn yêu cầu đăng nhập.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/not-a-real-endpoint")).StatusCode);

        // Endpoint [AllowAnonymous] vẫn công khai.
        var register = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Fallback Test",
                email = UniqueEmail("fallback"),
                password = ValidPassword,
                confirmPassword = ValidPassword
            });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
    }

    [Fact]
    public async Task Admin_can_look_up_user_by_email()
    {
        var (_, adminToken) = await CreateAdminAsync("admin-lookup");
        var email = UniqueEmail("lookup-target");
        var target = await _factory.CreateUserAsync(email, ValidPassword);
        var customerToken = await LoginAsync(email);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(
                HttpMethod.Get,
                $"/api/platform-admin/users?email={email}",
                customerToken)).StatusCode);

        var found = await SendAsync(
            HttpMethod.Get,
            $"/api/platform-admin/users?email={email.ToUpperInvariant()}",
            adminToken);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await ReadJsonAsync(found);
        Assert.Equal(target.Id, body.GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(
            [SystemRoles.Customer],
            body.GetProperty("systemRoles").EnumerateArray().Select(x => x.GetString()));

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                HttpMethod.Get,
                $"/api/platform-admin/users?email={UniqueEmail("missing")}",
                adminToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(
                HttpMethod.Get,
                "/api/platform-admin/users?email=not-an-email",
                adminToken)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_member_loses_assignments_and_organization_access()
    {
        var org = await CreateOrganizationWithStaffAsync();
        var eventId = await SeedEventAsync(org.OrganizationId, org.OwnerId);
        const string qrToken = "qr-token-deactivate";
        await SeedTicketAsync(eventId, org.OwnerId, qrToken);
        await AssignStaffAsync(org.OwnerToken, eventId, org.StaffMemberId);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ValidateAsync(org.StaffToken, eventId, qrToken)).StatusCode);

        var deactivate = await SendAsync(
            HttpMethod.Put,
            $"/api/organizations/{org.OrganizationId}/members/{org.StaffMemberId}/status",
            org.OwnerToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(org.StaffToken, eventId, qrToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                HttpMethod.Get,
                $"/api/organizations/{org.OrganizationId}",
                org.StaffToken)).StatusCode);

        // Mở khóa lại không tự khôi phục phân công cũ.
        await SendAsync(
            HttpMethod.Put,
            $"/api/organizations/{org.OrganizationId}/members/{org.StaffMemberId}/status",
            org.OwnerToken,
            new { isActive = true });
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ValidateAsync(org.StaffToken, eventId, qrToken)).StatusCode);
    }

    // ───────────────────── Quản lý thành viên ─────────────────────

    [Fact]
    public async Task Only_owner_manages_members_and_ownership_transfer_keeps_one_owner()
    {
        var org = await CreateOrganizationWithStaffAsync();
        var (managerToken, managerMemberId) = await AddMemberAsync(
            org.OrganizationId,
            org.OwnerToken,
            role: (int)OrganizationMemberRole.Manager);

        // Manager không được đổi vai trò hay chuyển quyền.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ChangeRoleAsync(managerToken, org.OrganizationId, org.StaffMemberId, 2)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await TransferAsync(managerToken, org.OrganizationId, managerMemberId)).StatusCode);

        // Owner không đổi được thành Owner qua đổi vai trò, không khóa được Owner.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await ChangeRoleAsync(org.OwnerToken, org.OrganizationId, org.StaffMemberId, 1)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await ChangeRoleAsync(org.OwnerToken, org.OrganizationId, org.OwnerMemberId, 3)).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeRoleAsync(org.OwnerToken, org.OrganizationId, org.StaffMemberId, 2)).StatusCode);

        // Không chuyển quyền cho chính mình hoặc thành viên tổ chức khác.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await TransferAsync(org.OwnerToken, org.OrganizationId, org.OwnerMemberId)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await TransferAsync(org.OwnerToken, org.OrganizationId, Guid.NewGuid())).StatusCode);

        var transfer = await TransferAsync(org.OwnerToken, org.OrganizationId, managerMemberId);
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal("Owner", (await ReadJsonAsync(transfer)).GetProperty("role").GetString());

        // Owner cũ thành Manager và mất quyền quản lý thành viên.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await ChangeRoleAsync(org.OwnerToken, org.OrganizationId, org.StaffMemberId, 3)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeRoleAsync(managerToken, org.OrganizationId, org.StaffMemberId, 3)).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var owners = await dbContext.OrganizationMembers
            .Where(x => x.OrganizationId == org.OrganizationId
                && x.Role == OrganizationMemberRole.Owner
                && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();
        Assert.Equal([managerMemberId], owners);
    }

    [Fact]
    public async Task Database_rejects_second_active_owner()
    {
        var org = await CreateOrganizationWithStaffAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var staff = await dbContext.OrganizationMembers.SingleAsync(x => x.Id == org.StaffMemberId);
        staff.Role = OrganizationMemberRole.Owner;

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    // ───────────────────── Tạo tổ chức: chỉ Organizer ─────────────────────

    [Fact]
    public async Task Only_organizer_granted_by_admin_can_create_organization()
    {
        var (_, adminToken) = await CreateAdminAsync("admin-organizer");
        var email = UniqueEmail("future-organizer");
        var user = await _factory.CreateUserAsync(email, ValidPassword);
        var customerToken = await LoginAsync(email);

        // Customer không được tạo tổ chức.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateOrganizationAsync(customerToken)).StatusCode);

        // PlatformAdmin cũng không: quyền quản trị không bao gồm làm ban tổ chức.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateOrganizationAsync(adminToken)).StatusCode);

        // Vai trò không tồn tại bị từ chối.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendAsync(
                HttpMethod.Put,
                $"/api/platform-admin/users/{user.Id}/system-role",
                adminToken,
                new { role = "SuperOrganizer" })).StatusCode);

        var granted = await SendAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{user.Id}/system-role",
            adminToken,
            new { role = "organizer" });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(
            [SystemRoles.Organizer],
            (await ReadJsonAsync(granted)).GetProperty("systemRoles")
                .EnumerateArray()
                .Select(x => x.GetString()));

        // Đổi vai trò làm token cũ hết hiệu lực; đăng nhập lại để nhận claim mới.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await CreateOrganizationAsync(customerToken)).StatusCode);
        var organizerToken = await LoginAsync(email);
        var created = await CreateOrganizationAsync(organizerToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var organizationId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        // Organizer vẫn đặt vé được như khách hàng (chỉ cần đăng nhập).
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Get, "/api/tickets/mine", organizerToken)).StatusCode);

        // Bị thu hồi Organizer: không tạo thêm tổ chức được,
        // nhưng vẫn là Owner của tổ chức đã tạo.
        await SendAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{user.Id}/system-role",
            adminToken,
            new { role = SystemRoles.Customer });
        var revokedToken = await LoginAsync(email);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateOrganizationAsync(revokedToken)).StatusCode);
        var stillOwner = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            revokedToken);
        Assert.Equal(HttpStatusCode.OK, stillOwner.StatusCode);
        Assert.Equal("Owner", (await ReadJsonAsync(stillOwner)).GetProperty("myRole").GetString());
    }

    // ───────────────────── Platform Admin ─────────────────────

    [Fact]
    public async Task Admin_whose_role_was_revoked_concurrently_cannot_act()
    {
        var (adminA, adminAToken) = await CreateAdminAsync("admin-a");
        var (adminB, _) = await CreateAdminAsync("admin-b");

        // Mô phỏng tranh chấp: B vừa hạ quyền A, nhưng JWT của A
        // vẫn còn hiệu lực trong khoảnh khắc đó (stamp chưa đổi).
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var adminRoleId = await dbContext.Roles
                .Where(x => x.Name == SystemRoles.PlatformAdmin)
                .Select(x => x.Id)
                .SingleAsync();
            await dbContext.UserRoles
                .Where(x => x.UserId == adminA && x.RoleId == adminRoleId)
                .ExecuteDeleteAsync();
        }

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{adminB}/system-role",
            adminAToken,
            new { role = SystemRoles.Customer });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var userManager = verifyScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var b = await userManager.FindByIdAsync(adminB.ToString());
        Assert.True(await userManager.IsInRoleAsync(b!, SystemRoles.PlatformAdmin));
    }

    [Fact]
    public async Task Admin_cannot_deactivate_organization_owner_until_ownership_moves()
    {
        var (_, adminToken) = await CreateAdminAsync("admin-owner-check");
        var org = await CreateOrganizationWithStaffAsync();

        var blocked = await SendAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{org.OwnerId}/status",
            adminToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await TransferAsync(org.OwnerToken, org.OrganizationId, org.StaffMemberId)).StatusCode);

        var allowed = await SendAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{org.OwnerId}/status",
            adminToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Bootstrapper_grants_first_admin_only_when_none_exists()
    {
        var firstEmail = UniqueEmail("bootstrap-first");
        var first = await _factory.CreateUserAsync(firstEmail, ValidPassword);
        await RunBootstrapperAsync(firstEmail);
        Assert.True(await IsAdminAsync(first.Id));

        // Đã có Admin → không cấp thêm cho email khác.
        var secondEmail = UniqueEmail("bootstrap-second");
        var second = await _factory.CreateUserAsync(secondEmail, ValidPassword);
        await RunBootstrapperAsync(secondEmail);
        Assert.False(await IsAdminAsync(second.Id));
    }

    // ───────────────────────── Helpers ─────────────────────────

    private sealed record OrganizationSetup(
        Guid OrganizationId,
        Guid OwnerId,
        Guid OwnerMemberId,
        string OwnerToken,
        Guid StaffMemberId,
        string StaffToken);

    private async Task<OrganizationSetup> CreateOrganizationWithStaffAsync()
    {
        var ownerEmail = UniqueEmail("owner");
        var owner = await _factory.CreateUserAsync(
            ownerEmail,
            ValidPassword,
            role: SystemRoles.Organizer);
        var ownerToken = await LoginAsync(ownerEmail);

        var created = await CreateOrganizationAsync(ownerToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var organizationId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var (staffToken, staffMemberId) = await AddMemberAsync(
            organizationId,
            ownerToken,
            role: (int)OrganizationMemberRole.Staff);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var ownerMemberId = await dbContext.OrganizationMembers
            .Where(x => x.OrganizationId == organizationId && x.UserId == owner.Id)
            .Select(x => x.Id)
            .SingleAsync();

        return new(organizationId, owner.Id, ownerMemberId, ownerToken, staffMemberId, staffToken);
    }

    private Task<HttpResponseMessage> CreateOrganizationAsync(string token) =>
        SendAsync(
            HttpMethod.Post,
            "/api/organizations",
            token,
            new
            {
                name = "Authorization Org",
                contactEmail = UniqueEmail("org"),
                contactPhone = "0900000000",
                address = "Ho Chi Minh City"
            });

    private async Task<(string Token, Guid MemberId)> AddMemberAsync(
        Guid organizationId,
        string ownerToken,
        int role)
    {
        var email = UniqueEmail("member");
        await _factory.CreateUserAsync(email, ValidPassword);
        var added = await SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/members",
            ownerToken,
            new { email, role });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var memberId = (await ReadJsonAsync(added)).GetProperty("id").GetGuid();
        return (await LoginAsync(email), memberId);
    }

    private async Task<Guid> SeedEventAsync(
        Guid organizationId,
        Guid createdByUserId,
        EventStatus status = EventStatus.Published)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var evt = new Event
        {
            OrganizationId = organizationId,
            CategoryId = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            Title = "Authorization Event",
            Slug = $"event-{Guid.NewGuid():N}",
            Description = "Test",
            VenueName = "Venue",
            Address = "Address",
            City = "HCM",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(2),
            Status = status,
            RowVersion = [1]
        };
        dbContext.Events.Add(evt);
        await dbContext.SaveChangesAsync();
        return evt.Id;
    }

    private async Task SeedTicketAsync(
        Guid eventId,
        Guid buyerId,
        string qrToken,
        OrderStatus orderStatus = OrderStatus.Paid)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
        var order = new Order
        {
            OrderCode = $"ORD-{Guid.NewGuid():N}"[..20],
            UserId = buyerId,
            EventId = eventId,
            ReservationId = Guid.NewGuid(),
            CustomerName = "Customer",
            CustomerEmail = "customer@example.com",
            CustomerPhone = "0900000000",
            TotalAmount = 100_000m,
            Status = orderStatus,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            PaidAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            RowVersion = [1]
        };
        var item = new OrderItem
        {
            OrderId = order.Id,
            TicketTypeId = Guid.NewGuid(),
            TicketTypeName = "Standard",
            UnitPrice = 100_000m,
            Quantity = 1
        };
        order.Items.Add(item);
        dbContext.Orders.Add(order);
        dbContext.Tickets.Add(new Ticket
        {
            OrderItemId = item.Id,
            TicketCode = $"TKT-{Guid.NewGuid():N}"[..20],
            QrTokenHash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(qrToken))),
            Status = TicketStatus.Valid,
            IssuedAt = DateTimeOffset.UtcNow,
            RowVersion = [1]
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<(Guid UserId, string Token)> CreateAdminAsync(string prefix)
    {
        var email = UniqueEmail(prefix);
        var user = await _factory.CreateUserAsync(
            email,
            ValidPassword,
            role: SystemRoles.PlatformAdmin);
        return (user.Id, await LoginAsync(email));
    }

    private async Task RunBootstrapperAsync(string email)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PlatformAdminBootstrapper.ConfigurationKey] = email
            })
            .Build();
        var bootstrapper = new PlatformAdminBootstrapper(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<PlatformAdminBootstrapper>.Instance);
        await bootstrapper.StartAsync(CancellationToken.None);
    }

    private async Task<bool> IsAdminAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return await userManager.IsInRoleAsync(user!, SystemRoles.PlatformAdmin);
    }

    private async Task<string> CreateUserAndLoginAsync(string prefix)
    {
        var email = UniqueEmail(prefix);
        await _factory.CreateUserAsync(email, ValidPassword);
        return await LoginAsync(email);
    }

    private Task<HttpResponseMessage> AssignStaffAsync(string token, Guid eventId, Guid memberId) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/staff",
            token,
            new { organizationMemberId = memberId });

    private Task<HttpResponseMessage> ValidateAsync(string token, Guid eventId, string qrToken) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/check-in/validate",
            token,
            new { qrToken });

    private Task<HttpResponseMessage> CheckInAsync(string token, Guid eventId, string qrToken) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/check-in",
            token,
            new { qrToken, gateName = "Gate A" });

    private Task<HttpResponseMessage> ChangeRoleAsync(
        string token,
        Guid organizationId,
        Guid memberId,
        int role) =>
        SendAsync(
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/members/{memberId}/role",
            token,
            new { role });

    private Task<HttpResponseMessage> TransferAsync(
        string token,
        Guid organizationId,
        Guid newOwnerMemberId) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/transfer-ownership",
            token,
            new { newOwnerMemberId });

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("accessToken").GetString()!;
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

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";
}
