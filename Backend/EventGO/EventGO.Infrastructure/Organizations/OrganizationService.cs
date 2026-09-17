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

    public OrganizationService(
        EventGoDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
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
        var membership = await _dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                    && x.UserId == currentUserId
                    && x.IsActive,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == organizationId && x.IsActive,
                cancellationToken);

        if (organization is null)
        {
            return null;
        }

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
        var membership = await _dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId
                    && x.UserId == currentUserId
                    && x.IsActive,
                cancellationToken);

        if (membership is null)
        {
            return (null, false);
        }

        var organization = await _dbContext.Organizations
            .SingleOrDefaultAsync(
                x => x.Id == organizationId && x.IsActive,
                cancellationToken);

        if (organization is null)
        {
            return (null, false);
        }

        if (membership.Role != OrganizationMemberRole.Owner
            && membership.Role != OrganizationMemberRole.Manager)
        {
            return (null, true);
        }

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
    private async Task<OrganizationMember?> GetActiveMembershipAsync(
    Guid organizationId,
    Guid currentUserId,
    CancellationToken cancellationToken)
    {
        return await (
            from member in _dbContext.OrganizationMembers.AsNoTracking()
            join organization in _dbContext.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where member.OrganizationId == organizationId
                && member.UserId == currentUserId
                && member.IsActive
                && organization.IsActive
            select member
        ).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationMemberResult<OrganizationMemberResponse>>
        AddMemberAsync(
            Guid organizationId,
            Guid currentUserId,
            AddOrganizationMemberRequest request,
            CancellationToken cancellationToken = default)
    {
        var actor = await GetActiveMembershipAsync(
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

        // Kiểm tra trong service để bảo vệ quy tắc nghiệp vụ,
        // kể cả khi phương thức được gọi ngoài Controller.
        if (request.Role != (int)OrganizationMemberRole.Manager
            && request.Role != (int)OrganizationMemberRole.Staff)
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
        // Khôi phục thành viên sẽ dùng chức năng riêng.
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
            when (exception.InnerException is SqlException sql
                && (sql.Number == 2601 || sql.Number == 2627))
        {
            // Hai yêu cầu đồng thời có thể cùng vượt qua AnyAsync.
            // Unique index trong database sẽ chặn bản ghi thứ hai.
            _dbContext.Entry(membership).State = EntityState.Detached;

            return new(null, OrganizationMemberError.AlreadyMember);
        }

        return new(
            new OrganizationMemberResponse
            {
                Id = membership.Id,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = membership.Role.ToString(),
                IsActive = membership.IsActive,
                JoinedAt = membership.JoinedAt
            },
            OrganizationMemberError.None);
    }

    public async Task<
        OrganizationMemberResult<List<OrganizationMemberResponse>>>
        GetMembersAsync(
            Guid organizationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
    {
        var actor = await GetActiveMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (actor is null)
        {
            return new(null, OrganizationMemberError.NotFound);
        }

        if (actor.Role != OrganizationMemberRole.Owner
            && actor.Role != OrganizationMemberRole.Manager)
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
}