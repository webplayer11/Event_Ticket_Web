using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EventGO.Tests;

public sealed class AuthenticationApiTests : IAsyncLifetime
{
    private const string ValidPassword = "Valid!Pass123";
    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.InitializeDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Register_handles_success_duplicate_and_validation()
    {
        var email = UniqueEmail("register");
        var success = await RegisterAsync(email, ValidPassword);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);

        var duplicate = await RegisterAsync(
            email.ToUpperInvariant(),
            ValidPassword);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.DoesNotContain(
            "tồn tại",
            await duplicate.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        var invalidEmail = await RegisterAsync("not-an-email", ValidPassword);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        var weakPassword = await RegisterAsync(
            UniqueEmail("weak"),
            "weakpass");
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, SystemRoles.Customer));
    }

    [Fact]
    public async Task Login_handles_success_wrong_unknown_inactive_and_locked()
    {
        var activeEmail = UniqueEmail("login");
        await _factory.CreateUserAsync(activeEmail, ValidPassword);

        var success = await LoginAsync(activeEmail, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var successfulBody = await success.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            SystemRoles.Customer,
            successfulBody.GetProperty("user")
                .GetProperty("systemRoles")
                .EnumerateArray()
                .Select(value => value.GetString()));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(activeEmail, "Wrong!Pass123")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(UniqueEmail("unknown"), ValidPassword)).StatusCode);

        var inactiveEmail = UniqueEmail("inactive");
        var inactive = await _factory.CreateUserAsync(
            inactiveEmail,
            ValidPassword,
            isActive: false);
        Assert.False(inactive.IsActive);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(inactiveEmail, ValidPassword)).StatusCode);

        var lockedEmail = UniqueEmail("locked");
        var locked = await _factory.CreateUserAsync(lockedEmail, ValidPassword);
        await _factory.SetLockoutAsync(locked.Id);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(lockedEmail, ValidPassword)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_is_neutral_and_validates_input()
    {
        var email = UniqueEmail("forgot");
        await _factory.CreateUserAsync(email, ValidPassword);

        var known = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
        var unknown = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = UniqueEmail("missing") });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await unknown.Content.ReadAsStringAsync());
        Assert.True(_factory.EmailSender.TryGetToken(email, out _));

        var invalid = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Reset_password_validates_token_password_and_single_use()
    {
        var email = UniqueEmail("reset");
        await _factory.CreateUserAsync(email, ValidPassword);
        await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
        Assert.True(_factory.EmailSender.TryGetToken(email, out var token));

        var invalidToken = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                email,
                token = "invalid-token",
                newPassword = "New!Valid123",
                confirmPassword = "New!Valid123"
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidToken.StatusCode);

        var weak = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                email,
                token,
                newPassword = "weakpass",
                confirmPassword = "weakpass"
            });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var valid = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                email,
                token,
                newPassword = "New!Valid123",
                confirmPassword = "New!Valid123"
            });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        var used = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                email,
                token,
                newPassword = "Other!Valid123",
                confirmPassword = "Other!Valid123"
            });
        Assert.Equal(HttpStatusCode.BadRequest, used.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(email, ValidPassword)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await LoginAsync(email, "New!Valid123")).StatusCode);
    }

    [Fact]
    public async Task Me_and_profile_update_enforce_auth_and_allowlist()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.PutAsJsonAsync(
                "/api/auth/me",
                new { fullName = "Blocked", phoneNumber = "+84901234567" }))
            .StatusCode);

        var email = UniqueEmail("profile");
        var user = await _factory.CreateUserAsync(email, ValidPassword);
        var token = await GetTokenAsync(email, ValidPassword);

        var me = await SendAuthorizedAsync(HttpMethod.Get, "/api/auth/me", token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var update = await SendAuthorizedAsync(
            HttpMethod.Put,
            "/api/auth/me",
            token,
            new
            {
                fullName = "Updated Name",
                phoneNumber = "+84901234567",
                isActive = false,
                systemRoles = new[] { SystemRoles.PlatformAdmin },
                email = "attacker@example.com"
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.FullName);
        Assert.Equal("+84901234567", updated.PhoneNumber);
        Assert.Equal(email, updated.Email);
        Assert.True(updated.IsActive);
        Assert.False(await userManager.IsInRoleAsync(
            updated,
            SystemRoles.PlatformAdmin));
    }

    [Fact]
    public async Task Change_password_revokes_old_jwt_and_requires_old_password()
    {
        var email = UniqueEmail("change");
        await _factory.CreateUserAsync(email, ValidPassword);
        var token = await GetTokenAsync(email, ValidPassword);

        var wrong = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/change-password",
            token,
            new
            {
                currentPassword = "Wrong!Pass123",
                newPassword = "Changed!Pass123",
                confirmPassword = "Changed!Pass123"
            });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var weak = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/change-password",
            token,
            new
            {
                currentPassword = ValidPassword,
                newPassword = "weakpass",
                confirmPassword = "weakpass"
            });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var changed = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/change-password",
            token,
            new
            {
                currentPassword = ValidPassword,
                newPassword = "Changed!Pass123",
                confirmPassword = "Changed!Pass123"
            });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var stale = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/auth/me",
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
    }

    [Fact]
    public async Task Logout_and_deactivation_revoke_existing_jwts()
    {
        var email = UniqueEmail("logout");
        var user = await _factory.CreateUserAsync(email, ValidPassword);
        var token = await GetTokenAsync(email, ValidPassword);

        var logout = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/logout",
            token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAuthorizedAsync(
                HttpMethod.Get,
                "/api/auth/me",
                token)).StatusCode);

        var freshToken = await GetTokenAsync(email, ValidPassword);
        await _factory.SetActiveAsync(user.Id, false);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAuthorizedAsync(
                HttpMethod.Get,
                "/api/auth/me",
                freshToken)).StatusCode);
    }

    [Fact]
    public async Task Platform_admin_policy_is_global_and_role_change_revokes_token()
    {
        var customerEmail = UniqueEmail("customer");
        var customer = await _factory.CreateUserAsync(
            customerEmail,
            ValidPassword);
        var customerToken = await GetTokenAsync(customerEmail, ValidPassword);
        var denied = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{Guid.NewGuid()}/system-role",
            customerToken,
            new { role = SystemRoles.Customer });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var adminEmail = UniqueEmail("admin");
        var admin = await _factory.CreateUserAsync(
            adminEmail,
            ValidPassword,
            role: SystemRoles.PlatformAdmin);
        var adminToken = await GetTokenAsync(adminEmail, ValidPassword);
        var assigned = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{customer.Id}/system-role",
            adminToken,
            new { role = SystemRoles.PlatformAdmin });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAuthorizedAsync(
                HttpMethod.Get,
                "/api/auth/me",
                customerToken)).StatusCode);

        var selfChange = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{admin.Id}/status",
            adminToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, selfChange.StatusCode);

        var deactivatedEmail = UniqueEmail("admin-deactivate");
        var deactivated = await _factory.CreateUserAsync(
            deactivatedEmail,
            ValidPassword);
        var deactivatedToken = await GetTokenAsync(
            deactivatedEmail,
            ValidPassword);
        var statusChanged = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{deactivated.Id}/status",
            adminToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, statusChanged.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendAuthorizedAsync(
                HttpMethod.Get,
                "/api/auth/me",
                deactivatedToken)).StatusCode);
    }

    [Fact]
    public async Task Organization_roles_do_not_grant_platform_or_cross_tenant_access()
    {
        var ownerEmail = UniqueEmail("orgowner");
        var owner = await _factory.CreateUserAsync(ownerEmail, ValidPassword);
        await _factory.AddMembershipInOtherOrganizationAsync(
            owner.Id,
            OrganizationMemberRole.Owner);
        var ownerToken = await GetTokenAsync(ownerEmail, ValidPassword);

        var platformDenied = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/platform-admin/users/{Guid.NewGuid()}/status",
            ownerToken,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.Forbidden, platformDenied.StatusCode);

        var staffEmail = UniqueEmail("orgstaff");
        var staff = await _factory.CreateUserAsync(staffEmail, ValidPassword);
        var organizationBId = await _factory.AddMembershipInOtherOrganizationAsync(
            staff.Id,
            OrganizationMemberRole.Staff);
        var staffToken = await GetTokenAsync(staffEmail, ValidPassword);

        var tenantDenied = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationBId}",
            staffToken);
        Assert.Equal(HttpStatusCode.NotFound, tenantDenied.StatusCode);
    }

    private Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password) =>
        _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Authentication Test",
                email,
                password,
                confirmPassword = password
            });

    private Task<HttpResponseMessage> LoginAsync(
        string email,
        string password) =>
        _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

    private async Task<string> GetTokenAsync(string email, string password)
    {
        var response = await LoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return await _client.SendAsync(request);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";
}

internal sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");

    public CapturingPasswordResetEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=EventGO_Tests;Trusted_Connection=True",
                ["Jwt:Issuer"] = "EventGO.Tests",
                ["Jwt:Audience"] = "EventGO.Tests",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:SecretKey"] = Convert.ToBase64String(
                    Enumerable.Range(1, 32).Select(value => (byte)value).ToArray())
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EventGoDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<EventGoDbContext>>();
            services.RemoveAll<EventGoDbContext>();
            services.AddSingleton(_connection);
            services.AddDbContext<EventGoDbContext>(
                options => options.UseSqlite(_connection));
            services.RemoveAll<EventGoDbContext>();
            services.AddScoped<EventGoDbContext>(provider =>
                new TestEventGoDbContext(
                    provider.GetRequiredService<
                        DbContextOptions<EventGoDbContext>>()));

            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(EmailSender);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await _connection.OpenAsync();
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<EventGoDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole<Guid>(role));
                Assert.True(result.Succeeded);
            }
        }
    }

    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        bool isActive = true,
        string role = SystemRoles.Customer)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            FullName = "Authentication Test",
            IsActive = isActive
        };
        var create = await userManager.CreateAsync(user, password);
        Assert.True(create.Succeeded);
        var addRole = await userManager.AddToRoleAsync(user, role);
        Assert.True(addRole.Succeeded);
        return user;
    }

    public async Task SetLockoutAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var result = await userManager.SetLockoutEndDateAsync(
            user,
            DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.True(result.Succeeded);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        Assert.True(result.Succeeded);
    }

    public async Task<Guid> AddMembershipInOtherOrganizationAsync(
        Guid userId,
        OrganizationMemberRole role)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<EventGoDbContext>();
        var organizationA = new Organization
        {
            Name = "Organization A",
            ContactEmail = UniqueOrganizationEmail()
        };
        var organizationB = new Organization
        {
            Name = "Organization B",
            ContactEmail = UniqueOrganizationEmail()
        };
        dbContext.AddRange(
            organizationA,
            organizationB,
            new OrganizationMember
            {
                OrganizationId = organizationA.Id,
                UserId = userId,
                Role = role
            });
        await dbContext.SaveChangesAsync();
        return organizationB.Id;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static string UniqueOrganizationEmail() =>
        $"organization-{Guid.NewGuid():N}@example.com";
}

internal sealed class CapturingPasswordResetEmailSender
    : IPasswordResetEmailSender
{
    private readonly ConcurrentDictionary<string, string> _tokens =
        new(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tokens[recipientEmail] = resetToken;
        return Task.CompletedTask;
    }

    public bool TryGetToken(string email, out string token) =>
        _tokens.TryGetValue(email, out token!);
}
