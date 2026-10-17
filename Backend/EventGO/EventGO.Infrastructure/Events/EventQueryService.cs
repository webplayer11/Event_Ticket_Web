using EventGO.Application.Authorization;
using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

public sealed class EventQueryService : IEventQueryService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IOrganizationAuthorizer _authorizer;
    private readonly TimeProvider _timeProvider;

    public EventQueryService(
        EventGoDbContext dbContext,
        IOrganizationAuthorizer authorizer,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
        _timeProvider = timeProvider;
    }

    public async Task<EventPage> SearchPublishedAsync(
        EventSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var events = PublishedEvents().Where(x => x.EndsAt > now);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim().ToLower();
            events = events.Where(x => x.Title.ToLower().Contains(keyword));
        }

        if (query.CategoryId is { } categoryId)
        {
            events = events.Where(x => x.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            events = events.Where(x => x.City.ToLower() == city);
        }

        if (query.From is { } from)
        {
            events = events.Where(x => x.StartsAt >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(x => x.StartsAt <= to);
        }

        if (query.MinPrice is not null || query.MaxPrice is not null)
        {
            var minPrice = query.MinPrice;
            var maxPrice = query.MaxPrice;
            // Giá của sự kiện = giá các loại vé đang mở; khớp nếu có ít nhất một loại vé.
            events = events.Where(evt => _dbContext.TicketTypes.Any(t =>
                t.EventId == evt.Id
                && t.IsActive
                && (minPrice == null || t.Price >= minPrice)
                && (maxPrice == null || t.Price <= maxPrice)));
        }

        var totalCount = await events.CountAsync(cancellationToken);

        var page = events
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        var items = (await EventReadModel.LoadAsync(_dbContext, page, cancellationToken))
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .Select(EventReadModel.ToPublic)
            .ToList();

        return new(items, totalCount);
    }

    public async Task<EventResponse?> GetDetailAsync(
        string? slug,
        Guid? eventId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var events = await EventReadModel.LoadAsync(
            _dbContext,
            eventId is { } id
                ? _dbContext.Events.Where(x => x.Id == id)
                : _dbContext.Events.Where(x => x.Slug == slug),
            cancellationToken);

        if (events.Count == 0)
        {
            return null;
        }

        var evt = events[0];
        if (evt.Status == nameof(EventStatus.Published)
            && await OrganizationIsActiveAsync(evt.OrganizationId, cancellationToken))
        {
            return EventReadModel.ToPublic(evt);
        }

        // Chưa công khai: chỉ Owner/Manager của tổ chức được xem (xem trước bản nháp).
        if (currentUserId is { } userId)
        {
            var membership = await _authorizer.GetMembershipAsync(
                evt.OrganizationId,
                userId,
                cancellationToken);

            if (membership is not null && OrganizationPermissions.CanManageEvents(membership.Role))
            {
                return EventReadModel.ToPublic(evt);
            }
        }

        return null;
    }

    public async Task<EventResult<List<ManagedEventResponse>>> GetByOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        EventStatus? status,
        CancellationToken cancellationToken = default)
    {
        var membership = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        if (membership is null)
        {
            return new(null, EventError.NotFound);
        }

        if (!OrganizationPermissions.CanManageEvents(membership.Role))
        {
            return new(null, EventError.Forbidden);
        }

        var events = _dbContext.Events.Where(x => x.OrganizationId == organizationId);
        if (status is { } filter)
        {
            events = events.Where(x => x.Status == filter);
        }

        var result = await EventReadModel.LoadAsync(_dbContext, events, cancellationToken);
        return new(result.OrderByDescending(x => x.CreatedAt).ToList(), EventError.None);
    }

    public async Task<List<ManagedEventResponse>> GetManagedAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var organizationIds = await (
            from member in _dbContext.OrganizationMembers.AsNoTracking()
            join organization in _dbContext.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where member.UserId == currentUserId
                && member.IsActive
                && organization.IsActive
                && (member.Role == OrganizationMemberRole.Owner
                    || member.Role == OrganizationMemberRole.Manager)
            select member.OrganizationId
        ).ToListAsync(cancellationToken);

        var events = await EventReadModel.LoadAsync(
            _dbContext,
            _dbContext.Events.Where(x => organizationIds.Contains(x.OrganizationId)),
            cancellationToken);

        return events.OrderByDescending(x => x.CreatedAt).ToList();
    }

    // Chỉ sự kiện đã công khai, thuộc tổ chức đang hoạt động.
    private IQueryable<Event> PublishedEvents() =>
        from evt in _dbContext.Events
        join organization in _dbContext.Organizations
            on evt.OrganizationId equals organization.Id
        where evt.Status == EventStatus.Published && organization.IsActive
        select evt;

    private Task<bool> OrganizationIsActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _dbContext.Organizations.AnyAsync(
            x => x.Id == organizationId && x.IsActive,
            cancellationToken);
}
