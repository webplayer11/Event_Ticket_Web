using EventGO.Application.Authorization;
using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

public sealed class EventService : IEventService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IOrganizationAuthorizer _authorizer;
    private readonly IEventApprovalNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public EventService(
        EventGoDbContext dbContext,
        IOrganizationAuthorizer authorizer,
        IEventApprovalNotifier notifier,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    public Task<List<EventCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        _dbContext.EventCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new EventCategoryResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug
            })
            .ToListAsync(cancellationToken);

    public async Task<EventResult<ManagedEventResponse>> CreateAsync(
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return new(null, EventError.Invalid, validationErrors);
        }

        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == currentUserId, cancellationToken);

        Guid organizationId;
        if (request.OrganizationId is { } requestedOrganizationId)
        {
            var membership = await _authorizer.GetMembershipAsync(
                requestedOrganizationId,
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

            organizationId = requestedOrganizationId;
        }
        else
        {
            var personalOrganizationId = await GetOrCreatePersonalOrganizationAsync(
                user,
                request,
                cancellationToken);

            if (personalOrganizationId is null)
            {
                // Tổ chức cá nhân đã bị khóa.
                return new(null, EventError.Forbidden);
            }

            organizationId = personalOrganizationId.Value;
        }

        var now = _timeProvider.GetUtcNow();
        var evt = new Event
        {
            OrganizationId = organizationId,
            CategoryId = request.CategoryId,
            CreatedByUserId = currentUserId,
            Slug = SlugGenerator.Create(request.Title),
            Status = EventStatus.PendingApproval,
            CreatedAt = now
        };
        ApplyFields(evt, request);

        _dbContext.Events.Add(evt);
        AddTicketTypes(evt.Id, request.TicketTypes);
        _dbContext.EventApprovalHistories.Add(new EventApprovalHistory
        {
            EventId = evt.Id,
            FromStatus = EventStatus.Draft,
            ToStatus = EventStatus.PendingApproval,
            ActionByUserId = currentUserId,
            Note = "Gửi duyệt sự kiện mới.",
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadManagedAsync(evt.Id, cancellationToken);
        await _notifier.NotifyAdminsAsync(
            ToNotification(response, isResubmission: false),
            cancellationToken);

        return new(response, EventError.None);
    }

    public async Task<EventResult<ManagedEventResponse>> UpdateAsync(
        Guid eventId,
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await _authorizer.GetMembershipForEventAsync(
            eventId,
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

        var evt = await _dbContext.Events
            .SingleAsync(x => x.Id == eventId, cancellationToken);

        // Sự kiện đã công khai có thể đã bán vé, không cho sửa tự do.
        if (evt.Status is not (EventStatus.PendingApproval or EventStatus.Rejected))
        {
            return new(null, EventError.InvalidStatus);
        }

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return new(null, EventError.Invalid, validationErrors);
        }

        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        var now = _timeProvider.GetUtcNow();
        var wasRejected = evt.Status == EventStatus.Rejected;

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            evt.CategoryId = request.CategoryId;
            evt.UpdatedAt = now;
            ApplyFields(evt, request);

            if (wasRejected)
            {
                evt.Status = EventStatus.PendingApproval;
                _dbContext.EventApprovalHistories.Add(new EventApprovalHistory
                {
                    EventId = evt.Id,
                    FromStatus = EventStatus.Rejected,
                    ToStatus = EventStatus.PendingApproval,
                    ActionByUserId = currentUserId,
                    Note = "Gửi duyệt lại sau khi chỉnh sửa.",
                    CreatedAt = now
                });
            }

            // Sự kiện chưa công khai nên chưa có giữ chỗ/đơn hàng:
            // thay toàn bộ loại vé. Xóa trước, lưu, rồi thêm mới để không
            // vướng unique index (EventId, Name).
            var oldTicketTypes = await _dbContext.TicketTypes
                .Where(x => x.EventId == evt.Id)
                .ToListAsync(cancellationToken);
            _dbContext.TicketTypes.RemoveRange(oldTicketTypes);
            await _dbContext.SaveChangesAsync(cancellationToken);

            AddTicketTypes(evt.Id, request.TicketTypes);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Admin vừa duyệt/từ chối sự kiện trong lúc người dùng sửa.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return new(null, EventError.Conflict);
        }

        var response = await LoadManagedAsync(evt.Id, cancellationToken);

        if (wasRejected)
        {
            await _notifier.NotifyAdminsAsync(
                ToNotification(response, isResubmission: true),
                cancellationToken);
        }

        return new(response, EventError.None);
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

    public async Task<List<EventResponse>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var events = await EventReadModel.LoadAsync(
            _dbContext,
            PublishedEvents(),
            cancellationToken);

        return events
            .Where(x => x.EndsAt > now)
            .OrderBy(x => x.StartsAt)
            .Select(EventReadModel.ToPublic)
            .ToList();
    }

    public async Task<EventResponse?> GetPublishedByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var events = await EventReadModel.LoadAsync(
            _dbContext,
            PublishedEvents().Where(x => x.Id == eventId),
            cancellationToken);

        return events.Count == 0 ? null : EventReadModel.ToPublic(events[0]);
    }

    // Chỉ sự kiện đã duyệt, thuộc tổ chức đang hoạt động.
    private IQueryable<Event> PublishedEvents() =>
        from evt in _dbContext.Events
        join organization in _dbContext.Organizations
            on evt.OrganizationId equals organization.Id
        where evt.Status == EventStatus.Published && organization.IsActive
        select evt;

    /// <summary>
    /// Tổ chức cá nhân của người dùng, tự tạo ở lần đầu.
    /// Trả null nếu tổ chức cá nhân đã bị khóa.
    /// </summary>
    private async Task<Guid?> GetOrCreatePersonalOrganizationAsync(
        Identity.ApplicationUser user,
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await FindPersonalOrganizationAsync(user.Id, cancellationToken);
        if (existing is not null)
        {
            return existing.Value.IsActive ? existing.Value.Id : null;
        }

        var organization = new Organization
        {
            Name = Truncate(
                string.IsNullOrWhiteSpace(request.OrganizerName)
                    ? user.FullName
                    : request.OrganizerName.Trim(),
                200),
            Description = request.OrganizerInfo?.Trim() ?? string.Empty,
            ContactEmail = user.Email ?? string.Empty,
            ContactPhone = user.PhoneNumber ?? string.Empty,
            Address = string.Empty,
            PersonalOwnerUserId = user.Id
        };

        _dbContext.Organizations.Add(organization);
        _dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = OrganizationMemberRole.Owner
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return organization.Id;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Hai sự kiện đầu tiên được tạo cùng lúc: request kia đã tạo
            // tổ chức cá nhân trước, dùng lại tổ chức đó.
            _dbContext.ChangeTracker.Clear();
            var created = await FindPersonalOrganizationAsync(user.Id, cancellationToken);
            return created is { IsActive: true } ? created.Value.Id : null;
        }
    }

    private async Task<(Guid Id, bool IsActive)?> FindPersonalOrganizationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.Organizations
            .AsNoTracking()
            .Where(x => x.PersonalOwnerUserId == userId)
            .Select(x => new { x.Id, x.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : (row.Id, row.IsActive);
    }

    private List<string> Validate(SaveEventRequest request)
    {
        var errors = new List<string>();
        var now = _timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("Thông tin sự kiện không được để trống.");
        }

        if (request.StartsAt <= now)
        {
            errors.Add("Thời gian bắt đầu phải ở tương lai.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            errors.Add("Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (request.TicketTypes.Count == 0)
        {
            errors.Add("Sự kiện phải có ít nhất một loại vé.");
        }

        var duplicateNames = request.TicketTypes
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            errors.Add($"Tên loại vé bị trùng: {string.Join(", ", duplicateNames)}.");
        }

        foreach (var ticketType in request.TicketTypes)
        {
            var name = ticketType.Name.Trim();
            if (ticketType.SaleEndsAt <= ticketType.SaleStartsAt)
            {
                errors.Add($"Loại vé \"{name}\": thời gian kết thúc bán phải sau thời gian mở bán.");
            }

            if (ticketType.SaleEndsAt > request.EndsAt)
            {
                errors.Add($"Loại vé \"{name}\": phải ngừng bán trước khi sự kiện kết thúc.");
            }

            if (ticketType.MaxQuantityPerOrder > ticketType.TotalQuantity)
            {
                errors.Add($"Loại vé \"{name}\": số vé tối đa mỗi đơn vượt quá tổng số vé.");
            }
        }

        return errors;
    }

    private Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken) =>
        _dbContext.EventCategories.AnyAsync(
            x => x.Id == categoryId && x.IsActive,
            cancellationToken);

    private static void ApplyFields(Event evt, SaveEventRequest request)
    {
        evt.Title = request.Title.Trim();
        evt.Description = request.Description.Trim();
        evt.VenueName = request.VenueName.Trim();
        evt.Address = request.Address.Trim();
        evt.City = request.City.Trim();
        evt.StartsAt = request.StartsAt;
        evt.EndsAt = request.EndsAt;
    }

    private void AddTicketTypes(Guid eventId, IEnumerable<TicketTypeRequest> ticketTypes)
    {
        foreach (var ticketType in ticketTypes)
        {
            _dbContext.TicketTypes.Add(new TicketType
            {
                EventId = eventId,
                Name = ticketType.Name.Trim(),
                Description = ticketType.Description?.Trim() ?? string.Empty,
                Price = ticketType.Price,
                Currency = "VND",
                TotalQuantity = ticketType.TotalQuantity,
                MaxQuantityPerOrder = ticketType.MaxQuantityPerOrder,
                SaleStartsAt = ticketType.SaleStartsAt,
                SaleEndsAt = ticketType.SaleEndsAt,
                IsActive = true
            });
        }
    }

    private async Task<ManagedEventResponse> LoadManagedAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        (await EventReadModel.LoadAsync(
            _dbContext,
            _dbContext.Events.Where(x => x.Id == eventId),
            cancellationToken)).Single();

    private static EventSubmittedNotification ToNotification(
        ManagedEventResponse evt,
        bool isResubmission) =>
        new(
            evt.Id,
            evt.Title,
            evt.OrganizationName,
            evt.CreatedByName,
            evt.CreatedByEmail,
            evt.StartsAt,
            isResubmission);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
