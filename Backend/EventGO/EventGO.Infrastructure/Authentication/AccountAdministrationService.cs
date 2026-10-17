using System.Data;
using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

    public async Task<AdminUserResponse?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            SystemRoles = roles.Order(StringComparer.Ordinal).ToArray(),
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };
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

        try
        {
            // Serializable: hai Admin hạ quyền nhau cùng lúc sẽ phải xếp hàng,
            // lệnh thứ hai thấy người gọi đã mất quyền và bị chặn.
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var guardError = await CheckAdminGuardsAsync(
                actorUserId,
                targetUserId,
                removesAdmin: canonicalRole != SystemRoles.PlatformAdmin,
                cancellationToken);

            if (guardError != AccountAdministrationError.None)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(guardError);
            }

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
        }
        catch (Exception exception) when (IsTransientConflict(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return new(AccountAdministrationError.Conflict);
        }

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

        try
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var guardError = await CheckAdminGuardsAsync(
                actorUserId,
                targetUserId,
                removesAdmin: !isActive,
                cancellationToken);

            if (guardError == AccountAdministrationError.None
                && !isActive
                && await OwnsActiveOrganizationAsync(targetUserId, cancellationToken))
            {
                guardError = AccountAdministrationError.OwnsActiveOrganizations;
            }

            if (guardError != AccountAdministrationError.None)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(guardError);
            }

            user.IsActive = isActive;
            user.SecurityStamp = Guid.NewGuid().ToString();
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(AccountAdministrationError.Conflict);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsTransientConflict(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return new(AccountAdministrationError.Conflict);
        }

        return new(AccountAdministrationError.None, await MapUserAsync(user));
    }

    /// <summary>
    /// Chạy trong transaction Serializable:
    /// 1. Người gọi phải vẫn là PlatformAdmin đang hoạt động (đọc lại từ DB,
    ///    không tin claim trong JWT vì có thể vừa bị hạ quyền).
    /// 2. Nếu thao tác tước quyền Admin của target (hạ quyền hoặc khóa),
    ///    phải còn ít nhất một Admin đang hoạt động khác.
    /// </summary>
    private async Task<AccountAdministrationError> CheckAdminGuardsAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool removesAdmin,
        CancellationToken cancellationToken)
    {
        var activeAdminIds = await GetActiveAdminIdsAsync(cancellationToken);

        if (!activeAdminIds.Contains(actorUserId))
        {
            return AccountAdministrationError.ActorNotAuthorized;
        }

        if (removesAdmin
            && activeAdminIds.Contains(targetUserId)
            && activeAdminIds.Count(id => id != targetUserId) == 0)
        {
            return AccountAdministrationError.LastPlatformAdmin;
        }

        return AccountAdministrationError.None;
    }

    private Task<List<Guid>> GetActiveAdminIdsAsync(
        CancellationToken cancellationToken)
    {
        var normalizedAdminRole = SystemRoles.PlatformAdmin.ToUpperInvariant();

        return (
            from userRole in _dbContext.UserRoles
            join identityRole in _dbContext.Roles
                on userRole.RoleId equals identityRole.Id
            join user in _dbContext.Users
                on userRole.UserId equals user.Id
            where identityRole.NormalizedName == normalizedAdminRole
                && user.IsActive
            select user.Id
        ).ToListAsync(cancellationToken);
    }

    private Task<bool> OwnsActiveOrganizationAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from member in _dbContext.OrganizationMembers
            join organization in _dbContext.Organizations
                on member.OrganizationId equals organization.Id
            where member.UserId == userId
                && member.Role == OrganizationMemberRole.Owner
                && member.IsActive
                && organization.IsActive
            select member.Id
        ).AnyAsync(cancellationToken);

    /// <summary>
    /// SQL Server chọn một transaction Serializable làm deadlock victim (1205)
    /// khi hai thao tác tranh nhau. Coi như xung đột, client thử lại.
    /// </summary>
    private static bool IsTransientConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
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

    // Trả về tên vai trò chuẩn (đúng hoa/thường) hoặc null nếu không hợp lệ.
    private static string? GetCanonicalRole(string role) =>
        SystemRoles.All.FirstOrDefault(canonical => string.Equals(
            canonical,
            role.Trim(),
            StringComparison.OrdinalIgnoreCase));
}
