using EventGO.Application.Authorization;
using EventGO.Application.Organizations;
using EventGO.Application.Organizations.Dtos;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EventGO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;


namespace EventGO.Infrastructure.Organizations;

public class OrganizationService : IOrganizationService
{
    private readonly EventGoDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOrganizationAuthorizer _authorizer;

    public OrganizationService(
        EventGoDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IOrganizationAuthorizer authorizer)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _authorizer = authorizer;
    }

    public async Task<OrganizationResponse> CreateAsync(
        Guid currentUserId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var organization = new Organization
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            Address = request.Address.Trim()
        };

        var membership = new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = currentUserId,
            Role = OrganizationMemberRole.Owner
        };

        _dbContext.Organizations.Add(organization);
        _dbContext.OrganizationMembers.Add(membership);

        // Một lần SaveChanges: tổ chức và thành viên cùng được lưu,
        // hoặc cùng rollback nếu thao tác thất bại.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OrganizationResponse
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            ContactEmail = organization.ContactEmail,
            ContactPhone = organization.ContactPhone,
            Address = organization.Address,
            MyRole = membership.Role.ToString(),
            CreatedAt = organization.CreatedAt
        };
    }

    public async Task<List<OrganizationResponse>> GetMineAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from member in _dbContext.OrganizationMembers.AsNoTracking()
            join organization in _dbContext.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where member.UserId == currentUserId
                && member.IsActive
                && organization.IsActive
            orderby organization.CreatedAt descending
            select new
            {
                organization.Id,
                organization.Name,
                organization.Description,
                organization.ContactEmail,
                organization.ContactPhone,
                organization.Address,
                organization.CreatedAt,
                member.Role
            }
        ).ToListAsync(cancellationToken);

        return rows.Select(row => new OrganizationResponse
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            ContactEmail = row.ContactEmail,
            ContactPhone = row.ContactPhone,
            Address = row.Address,
            MyRole = row.Role.ToString(),
            CreatedAt = row.CreatedAt
        }).ToList();
    }

    public async Task<OrganizationResponse?> GetByIdAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == organizationId, cancellationToken);

        return MapOrganization(organization, membership.Role);
    }

    public async Task<(
        OrganizationResponse? Organization,
        bool Forbidden)> UpdateAsync(
            Guid organizationId,
            Guid currentUserId,
            UpdateOrganizationRequest request,
            CancellationToken cancellationToken = default)
    {
        var membership = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (membership is null)
        {
            return (null, false);
        }

        if (!OrganizationPermissions.CanManageOrganization(membership.Role))
        {
            return (null, true);
        }

        var organization = await _dbContext.Organizations
            .SingleAsync(x => x.Id == organizationId, cancellationToken);

        organization.Name = request.Name.Trim();
        organization.Description =
            request.Description?.Trim() ?? string.Empty;
        organization.ContactEmail = request.ContactEmail.Trim();
        organization.ContactPhone = request.ContactPhone.Trim();
        organization.Address = request.Address.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (MapOrganization(organization, membership.Role), false);
    }

    private static OrganizationResponse MapOrganization(
        Organization organization,
        OrganizationMemberRole role)
    {
        return new OrganizationResponse
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            ContactEmail = organization.ContactEmail,
            ContactPhone = organization.ContactPhone,
            Address = organization.Address,
            MyRole = role.ToString(),
            CreatedAt = organization.CreatedAt
        };
    }

    public async Task<OrganizationMemberResult<OrganizationMemberResponse>>
        AddMemberAsync(
            Guid organizationId,
            Guid currentUserId,
            AddOrganizationMemberRequest request,
            CancellationToken cancellationToken = default)
    {
        var actor = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (actor is null)
        {
            return new(null, OrganizationMemberError.NotFound);
        }

        if (!OrganizationPermissions.CanManageMembers(actor.Role))
        {
            return new(null, OrganizationMemberError.Forbidden);
        }

        // Kiểm tra trong service để bảo vệ quy tắc nghiệp vụ,
        // kể cả khi phương thức được gọi ngoài Controller.
        if (!IsAssignableRole(request.Role))
        {
            return new(null, OrganizationMemberError.InvalidRole);
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return new(null, OrganizationMemberError.UserNotFound);
        }

        var alreadyMember = await _dbContext.OrganizationMembers
            .AnyAsync(
                x => x.OrganizationId == organizationId
                    && x.UserId == user.Id,
                cancellationToken);

        // Bao gồm cả thành viên đã bị khóa.
        // Mở khóa thành viên dùng chức năng cập nhật trạng thái.
        if (alreadyMember)
        {
            return new(null, OrganizationMemberError.AlreadyMember);
        }

        var membership = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            Role = (OrganizationMemberRole)request.Role,
            IsActive = true
        };

        _dbContext.OrganizationMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            // Hai yêu cầu đồng thời có thể cùng vượt qua AnyAsync.
            // Unique index trong database sẽ chặn bản ghi thứ hai.
            _dbContext.Entry(membership).State = EntityState.Detached;

            return new(null, OrganizationMemberError.AlreadyMember);
        }

        return new(
            MapMember(membership, user),
            OrganizationMemberError.None);
    }

    public async Task<
        OrganizationMemberResult<List<OrganizationMemberResponse>>>
        GetMembersAsync(
            Guid organizationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
    {
        var actor = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (actor is null)
        {
            return new(null, OrganizationMemberError.NotFound);
        }

        if (!OrganizationPermissions.CanManageOrganization(actor.Role))
        {
            return new(null, OrganizationMemberError.Forbidden);
        }

        var rows = await (
            from member in _dbContext.OrganizationMembers.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.OrganizationId == organizationId
            orderby member.JoinedAt, member.Id
            select new
            {
                member.Id,
                member.UserId,
                user.FullName,
                user.Email,
                member.Role,
                member.IsActive,
                member.JoinedAt
            }
        ).ToListAsync(cancellationToken);

        var members = rows.Select(row => new OrganizationMemberResponse
        {
            Id = row.Id,
            UserId = row.UserId,
            FullName = row.FullName,
            Email = row.Email ?? string.Empty,
            Role = row.Role.ToString(),
            IsActive = row.IsActive,
            JoinedAt = row.JoinedAt
        }).ToList();

        return new(members, OrganizationMemberError.None);
    }

    public async Task<OrganizationMemberResult<OrganizationMemberResponse>>
        UpdateMemberRoleAsync(
            Guid organizationId,
            Guid currentUserId,
            Guid memberId,
            UpdateOrganizationMemberRoleRequest request,
            CancellationToken cancellationToken = default)
    {
        var (target, error) = await LoadManageableMemberAsync(
            organizationId,
            currentUserId,
            memberId,
            cancellationToken);

        if (target is null)
        {
            return new(null, error);
        }

        if (!IsAssignableRole(request.Role))
        {
            return new(null, OrganizationMemberError.InvalidRole);
        }

        target.Role = (OrganizationMemberRole)request.Role;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            await MapMemberAsync(target, cancellationToken),
            OrganizationMemberError.None);
    }

    public async Task<OrganizationMemberResult<OrganizationMemberResponse>>
        UpdateMemberStatusAsync(
            Guid organizationId,
            Guid currentUserId,
            Guid memberId,
            UpdateOrganizationMemberStatusRequest request,
            CancellationToken cancellationToken = default)
    {
        var (target, error) = await LoadManageableMemberAsync(
            organizationId,
            currentUserId,
            memberId,
            cancellationToken);

        if (target is null)
        {
            return new(null, error);
        }

        target.IsActive = request.IsActive;

        if (!request.IsActive)
        {
            // Thành viên bị khóa không còn được soát vé ở sự kiện nào.
            var assignments = await _dbContext.EventStaffAssignments
                .Where(x => x.OrganizationMemberId == target.Id && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var assignment in assignments)
            {
                assignment.IsActive = false;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            await MapMemberAsync(target, cancellationToken),
            OrganizationMemberError.None);
    }

    public async Task<OrganizationMemberResult<OrganizationMemberResponse>>
        TransferOwnershipAsync(
            Guid organizationId,
            Guid currentUserId,
            TransferOrganizationOwnershipRequest request,
            CancellationToken cancellationToken = default)
    {
        var actor = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (actor is null)
        {
            return new(null, OrganizationMemberError.NotFound);
        }

        if (actor.Role != OrganizationMemberRole.Owner)
        {
            return new(null, OrganizationMemberError.Forbidden);
        }

        if (request.NewOwnerMemberId == actor.MemberId)
        {
            return new(null, OrganizationMemberError.InvalidTransferTarget);
        }

        var target = await _dbContext.OrganizationMembers
            .SingleOrDefaultAsync(
                x => x.Id == request.NewOwnerMemberId
                    && x.OrganizationId == organizationId,
                cancellationToken);

        if (target is null)
        {
            return new(null, OrganizationMemberError.MemberNotFound);
        }

        var targetUserActive = await _dbContext.Users
            .AnyAsync(x => x.Id == target.UserId && x.IsActive, cancellationToken);

        if (!target.IsActive || !targetUserActive)
        {
            return new(null, OrganizationMemberError.InvalidTransferTarget);
        }

        var currentOwner = await _dbContext.OrganizationMembers
            .SingleAsync(x => x.Id == actor.MemberId, cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Hạ Owner cũ trước rồi mới nâng Owner mới, để không lúc nào
            // vi phạm unique index "mỗi tổ chức một Owner đang hoạt động".
            currentOwner.Role = OrganizationMemberRole.Manager;
            await _dbContext.SaveChangesAsync(cancellationToken);

            target.Role = OrganizationMemberRole.Owner;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            // Một yêu cầu chuyển quyền khác chạy đồng thời đã thành công trước.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            return new(null, OrganizationMemberError.Conflict);
        }

        return new(
            await MapMemberAsync(target, cancellationToken),
            OrganizationMemberError.None);
    }

    /// <summary>
    /// Kiểm tra actor là Owner và tải thành viên cần sửa.
    /// Owner không bị sửa vai trò hay trạng thái qua chức năng này.
    /// </summary>
    private async Task<(OrganizationMember? Member, OrganizationMemberError Error)>
        LoadManageableMemberAsync(
            Guid organizationId,
            Guid currentUserId,
            Guid memberId,
            CancellationToken cancellationToken)
    {
        var actor = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (actor is null)
        {
            return (null, OrganizationMemberError.NotFound);
        }

        if (!OrganizationPermissions.CanManageMembers(actor.Role))
        {
            return (null, OrganizationMemberError.Forbidden);
        }

        var target = await _dbContext.OrganizationMembers
            .SingleOrDefaultAsync(
                x => x.Id == memberId && x.OrganizationId == organizationId,
                cancellationToken);

        if (target is null)
        {
            return (null, OrganizationMemberError.MemberNotFound);
        }

        if (target.Role == OrganizationMemberRole.Owner)
        {
            return (null, OrganizationMemberError.CannotModifyOwner);
        }

        return (target, OrganizationMemberError.None);
    }

    private async Task<OrganizationMemberResponse> MapMemberAsync(
        OrganizationMember member,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == member.UserId, cancellationToken);

        return MapMember(member, user);
    }

    private static OrganizationMemberResponse MapMember(
        OrganizationMember member,
        ApplicationUser user) =>
        new()
        {
            Id = member.Id,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = member.Role.ToString(),
            IsActive = member.IsActive,
            JoinedAt = member.JoinedAt
        };

    private static bool IsAssignableRole(int role) =>
        role == (int)OrganizationMemberRole.Manager
        || role == (int)OrganizationMemberRole.Staff;

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is 2601 or 2627;
}
