using System.Security.Cryptography;
using System.Text;
using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Authentication;

public sealed class RefreshTokenService(EventGoDbContext db, UserManager<ApplicationUser> users,
    JwtTokenService jwt, TimeProvider time, IOptions<JwtOptions> options) : IRefreshTokenService
{
    public async Task<AuthResponse> CreateSessionAsync(ApplicationUser user, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var response = await jwt.CreateTokenAsync(user);
        AddToken(user, now, now.AddDays(options.Value.RefreshTokenDays), response);
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse?> RefreshAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 64 or > 128) return null;
        var hash = Hash(token);
        // No tracked entity and no read lock held while competing requests claim.
        var existing = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        var now = time.GetUtcNow();
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= now) return null;
        var user = await users.FindByIdAsync(existing.UserId.ToString());
        if (user is null || !user.IsActive || user.SecurityStamp != existing.SecurityStamp || await users.IsLockedOutAsync(user)) return null;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // One atomic claim across all backend instances. The losing request
        // cannot insert a second successor. Failure rolls back claim + insert.
        var claimed = await db.RefreshTokens.Where(x => x.Id == existing.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, (DateTimeOffset?)now), cancellationToken);
        if (claimed != 1) return null;
        var response = await jwt.CreateTokenAsync(user);
        // Absolute session expiry inherited, never extended on rotation.
        AddToken(user, now, existing.ExpiresAt, response);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return response;
    }

    private void AddToken(ApplicationUser user, DateTimeOffset now, DateTimeOffset expires, AuthResponse response)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = Hash(raw), SecurityStamp = user.SecurityStamp!,
            CreatedAt = now, ExpiresAt = expires
        });
        response.RefreshToken = raw;
        response.RefreshTokenExpiresAt = expires;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
