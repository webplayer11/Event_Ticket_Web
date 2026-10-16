using EventGO.Application.Authorization;
using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

public sealed class EventStaffService : IEventStaffService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IOrganizationAuthorizer _authorizer;
    private readonly TimeProvider _timeProvider;

    public EventStaffService(
        EventGoDbContext dbContext,
        IOrganizationAuthorizer authorizer,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
        _timeProvider = timeProvider;
    }

    public async Task<EventStaffResult<List<EventStaffAssignmentResponse>>> GetAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync(eventId, currentUserId, cancellationToken);
        if (error != EventStaffError.None)
        {
            return new(null, error);
        }

        var assignments = await (
            from assignment in _dbContext.EventStaffAssignments.AsNoTracking()
            join member in _dbContext.OrganizationMembers.AsNoTracking()
                on assignment.OrganizationMemberId equals member.Id
            join user in _dbContext.Users.AsNoTracking()
                on member.UserId equals user.Id
            where assignment.EventId == eventId
            orderby assignment.AssignedAt, assignment.Id
            select new EventStaffAssignmentResponse
            {
                Id = assignment.Id,
                EventId = assignment.EventId,
                OrganizationMemberId = member.Id,
                UserId = user.Id,
                FullName = user.FullName,
                Role = member.Role.ToString(),
                IsActive = assignment.IsActive,
                AssignedAt = assignment.AssignedAt
            }
        ).ToListAsync(cancellationToken);

        return new(assignments, EventStaffError.None);
    }

    public async Task<EventStaffResult<EventStaffAssignmentResponse>> AssignAsync(
        Guid eventId,
        Guid currentUserId,
        AssignEventStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync(eventId, currentUserId, cancellationToken);
        if (error != EventStaffError.None)
        {
            return new(null, error);
        }

        var organizationId = await _dbContext.Events
            .Where(x => x.Id == eventId)
            .Select(x => x.OrganizationId)
            .SingleAsync(cancellationToken);

        var memberIsAssignable = await _dbContext.OrganizationMembers
            .AnyAsync(
                x => x.Id == request.OrganizationMemberId
                    && x.OrganizationId == organizationId
                    && x.IsActive,
                cancellationToken);

        if (!memberIsAssignable)
        {
            return new(null, EventStaffError.MemberNotFound);
        }

        // Unique index (EventId, OrganizationMemberId): kích hoạt lại
        // phân công cũ thay vì tạo bản ghi mới.
        var assignment = await _dbContext.EventStaffAssignments
            .SingleOrDefaultAsync(
                x => x.EventId == eventId
                    && x.OrganizationMemberId == request.OrganizationMemberId,
                cancellationToken);

        if (assignment is null)
        {
            assignment = new EventStaffAssignment
            {
                EventId = eventId,
                OrganizationMemberId = request.OrganizationMemberId,
                AssignedByUserId = currentUserId,
                AssignedAt = _timeProvider.GetUtcNow()
            };
            _dbContext.EventStaffAssignments.Add(assignment);
        }
        else if (!assignment.IsActive)
        {
            assignment.IsActive = true;
            assignment.AssignedByUserId = currentUserId;
            assignment.AssignedAt = _timeProvider.GetUtcNow();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            await MapAsync(assignment.Id, cancellationToken),
            EventStaffError.None);
    }

    public async Task<EventStaffResult<EventStaffAssignmentResponse>> RevokeAsync(
        Guid eventId,
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var error = await AuthorizeAsync(eventId, currentUserId, cancellationToken);
        if (error != EventStaffError.None)
        {
            return new(null, error);
        }

        var assignment = await _dbContext.EventStaffAssignments
            .SingleOrDefaultAsync(
                x => x.Id == assignmentId && x.EventId == eventId,
                cancellationToken);

        if (assignment is null)
        {
            return new(null, EventStaffError.AssignmentNotFound);
        }

        // Chỉ thu hồi (IsActive = false), không xóa: CheckIns tham chiếu
        // tới phân công để biết ai đã soát vé.
        assignment.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            await MapAsync(assignment.Id, cancellationToken),
            EventStaffError.None);
    }

    private async Task<EventStaffError> AuthorizeAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var membership = await _authorizer.GetMembershipForEventAsync(
            eventId,
            currentUserId,
            cancellationToken);

        if (membership is null)
        {
            return EventStaffError.NotFound;
        }

        return OrganizationPermissions.CanManageEvents(membership.Role)
            ? EventStaffError.None
            : EventStaffError.Forbidden;
    }

    private Task<EventStaffAssignmentResponse> MapAsync(
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        (
            from assignment in _dbContext.EventStaffAssignments.AsNoTracking()
            join member in _dbContext.OrganizationMembers.AsNoTracking()
                on assignment.OrganizationMemberId equals member.Id
            join user in _dbContext.Users.AsNoTracking()
                on member.UserId equals user.Id
            where assignment.Id == assignmentId
            select new EventStaffAssignmentResponse
            {
                Id = assignment.Id,
                EventId = assignment.EventId,
                OrganizationMemberId = member.Id,
                UserId = user.Id,
                FullName = user.FullName,
                Role = member.Role.ToString(),
                IsActive = assignment.IsActive,
                AssignedAt = assignment.AssignedAt
            }
        ).SingleAsync(cancellationToken);
}
