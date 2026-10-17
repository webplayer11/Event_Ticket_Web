using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using EventGO.Application.Authentication;
using EventGO.Infrastructure.Authentication;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EventGO.Tests;

public sealed class TokenLifecycleTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new();
    private HttpClient client = null!;
    public async ValueTask InitializeAsync()
    {
        client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
    }
    public async ValueTask DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }

    [Fact]
    public async Task Login_rotation_hash_storage_absolute_expiry_and_logout_are_consistent()
    {
        var original = await LoginAsync();
        var raw = original.GetProperty("refreshToken").GetString()!;
        Assert.Equal(64, Convert.FromBase64String(raw).Length);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var stored = Assert.Single(await db.RefreshTokens.ToListAsync());
            Assert.NotEqual(raw, stored.TokenHash);
            Assert.Equal(64, stored.TokenHash.Length);
        }
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = raw, userId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(raw, next.GetProperty("refreshToken").GetString());
        Assert.Equal(original.GetProperty("user").GetProperty("id").GetGuid(), next.GetProperty("user").GetProperty("id").GetGuid());
        // SQLite's test-only DateTimeOffset binary converter truncates sub-ms ticks.
        Assert.InRange((original.GetProperty("refreshTokenExpiresAt").GetDateTimeOffset() - next.GetProperty("refreshTokenExpiresAt").GetDateTimeOffset()).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = raw })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(raw)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(next.GetProperty("accessToken").GetString()!)).StatusCode);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Authorization = new("Bearer", next.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(next.GetProperty("accessToken").GetString()!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = next.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("password")]
    public async Task Invalid_sessions_cannot_refresh(string condition)
    {
        var body = await LoginAsync();
        var id = body.GetProperty("user").GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventGoDbContext>();
            var token = await db.RefreshTokens.SingleAsync();
            if (condition == "expired")
            {
                token.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2);
                token.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            }
            if (condition == "revoked") token.RevokedAt = DateTimeOffset.UtcNow;
            if (condition == "password")
            {
                var user = await db.Users.SingleAsync(x => x.Id == id);
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
            await db.SaveChangesAsync();
        }
        if (condition == "inactive") await factory.SetActiveAsync(id, false);
        if (condition == "locked") await factory.SetLockoutAsync(id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = body.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Theory]
    [InlineData("valid", HttpStatusCode.OK)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("signature", HttpStatusCode.Unauthorized)]
    [InlineData("issuer", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("algorithm", HttpStatusCode.Unauthorized)]
    [InlineData("unsigned", HttpStatusCode.Unauthorized)]
    [InlineData("future", HttpStatusCode.Unauthorized)]
    public async Task Jwt_middleware_validates_signature_lifetime_issuer_audience_and_algorithm(string scenario, HttpStatusCode status)
    {
        var login = await LoginAsync();
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(login.GetProperty("accessToken").GetString());
        Assert.Contains(parsed.Claims, x => x.Type == "jti");
        Assert.Contains(parsed.Claims, x => x.Type == "iat");
        Assert.Contains(parsed.Claims, x => x.Type == ClaimTypes.Role && x.Value == SystemRoles.Customer);
        Assert.Equal(login.GetProperty("user").GetProperty("id").GetString(), parsed.Subject);
        Assert.InRange((parsed.ValidTo - parsed.ValidFrom).TotalMinutes, 14.9, 15.1);
        var opts = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var key = scenario == "signature" ? RandomNumberGenerator.GetBytes(64) : Convert.FromBase64String(opts.SecretKey);
        // HS384 requires a longer key, but remains disallowed by the validator.
        if (scenario == "algorithm") key = key.Concat(key).ToArray();
        var token = new JwtSecurityToken(
            issuer: scenario == "issuer" ? "wrong" : opts.Issuer,
            audience: scenario == "audience" ? "wrong" : opts.Audience,
            claims: parsed.Claims.Where(x => x.Type is not "exp" and not "nbf" and not "iss" and not "aud"),
            notBefore: scenario == "expired" ? DateTime.UtcNow.AddHours(-2) : scenario == "future" ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddMinutes(-1),
            expires: scenario == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(2),
            signingCredentials: scenario == "unsigned" ? null : new SigningCredentials(new SymmetricSecurityKey(key), scenario == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        Assert.Equal(status, (await MeAsync(new JwtSecurityTokenHandler().WriteToken(token))).StatusCode);
    }

    private async Task<JsonElement> LoginAsync()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        await factory.CreateUserAsync(email, "Valid!Pass123");
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Valid!Pass123" });
        login.EnsureSuccessStatusCode();
        return await login.Content.ReadFromJsonAsync<JsonElement>();
    }
    private Task<HttpResponseMessage> MeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }
}
