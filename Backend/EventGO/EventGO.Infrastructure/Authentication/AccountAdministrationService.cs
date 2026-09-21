using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace EventGO.Infrastructure.Authentication;

public sealed class AccountAdministrationService
    : IAccountAdministrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EventGoDbContext _dbContext;

    public AccountAdministrationService(
        UserManager<ApplicationUser> userManager,
        EventGoDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<AccountAdministrationResult> AssignSystemRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId)
        {
            return new(AccountAdministrationError.SelfModificationNotAllowed);
        }

        var canonicalRole = GetCanonicalRole(role);
        if (canonicalRole is null)
        {
            return new(AccountAdministrationError.InvalidRole);
        }

        var user = await _userManager.FindByIdAsync(targetUserId.ToString());
        if (user is null)
        {
            return new(AccountAdministrationError.UserNotFound);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(SystemRoles.All.Contains)
            .Where(current => !string.Equals(
                current,
                canonicalRole,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user,
                rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(AccountAdministrationError.Conflict);
            }
        }

        if (!currentRoles.Contains(
            canonicalRole,
            StringComparer.OrdinalIgnoreCase))
        {
            var addResult = await _userManager.AddToRoleAsync(
                user,
                canonicalRole);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(AccountAdministrationError.Conflict);
            }
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(AccountAdministrationError.Conflict);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(AccountAdministrationError.None, await MapUserAsync(user));
    }

    public async Task<AccountAdministrationResult> UpdateAccountStatusAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId)
        {
            return new(AccountAdministrationError.SelfModificationNotAllowed);
        }

        var user = await _userManager.FindByIdAsync(targetUserId.ToString());
        if (user is null)
        {
            return new(AccountAdministrationError.UserNotFound);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.IsActive = isActive;
        user.SecurityStamp = Guid.NewGuid().ToString();
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(AccountAdministrationError.Conflict);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(AccountAdministrationError.None, await MapUserAsync(user));
    }

    private async Task<UserResponse> MapUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            SystemRoles = roles.Order(StringComparer.Ordinal).ToArray(),
            CreatedAt = user.CreatedAt
        };
    }

    private static string? GetCanonicalRole(string role)
    {
        if (string.Equals(
            role.Trim(),
            SystemRoles.Customer,
            StringComparison.OrdinalIgnoreCase))
        {
            return SystemRoles.Customer;
        }

        if (string.Equals(
            role.Trim(),
            SystemRoles.PlatformAdmin,
            StringComparison.OrdinalIgnoreCase))
        {
            return SystemRoles.PlatformAdmin;
        }

        return null;
    }
}
