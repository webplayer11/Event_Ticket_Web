using EventGO.Application.Authorization;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Authorization;

public sealed class OrganizationAuthorizer : IOrganizationAuthorizer
{
    private readonly EventGoDbContext _dbContext;

    public OrganizationAuthorizer(EventGoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrganizationMembership?> GetMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (
            from member in _dbContext.OrganizationMembers.AsNoTracking()
            join organization in _dbContext.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where member.OrganizationId == organizationId
                && member.UserId == userId
                && member.IsActive
                && organization.IsActive
            select new OrganizationMembership(
                member.Id,
                member.OrganizationId,
                member.Role)
        ).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationMembership?> GetMembershipForEventAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organizationId = await _dbContext.Events
            .AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => (Guid?)x.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        return organizationId is null
            ? null
            : await GetMembershipAsync(
                organizationId.Value,
                userId,
                cancellationToken);
    }

    public Task<Guid?> GetActiveStaffAssignmentIdAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (
            from assignment in _dbContext.EventStaffAssignments.AsNoTracking()
            join member in _dbContext.OrganizationMembers.AsNoTracking()
                on assignment.OrganizationMemberId equals member.Id
            join evt in _dbContext.Events.AsNoTracking()
                on assignment.EventId equals evt.Id
            join organization in _dbContext.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where assignment.EventId == eventId
                && assignment.IsActive
                && member.UserId == userId
                && member.IsActive
                // Phân công phải thuộc đúng tổ chức sở hữu sự kiện.
                && member.OrganizationId == evt.OrganizationId
                && organization.IsActive
            select (Guid?)assignment.Id
        ).SingleOrDefaultAsync(cancellationToken);
    }
}
