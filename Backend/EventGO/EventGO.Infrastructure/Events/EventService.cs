using EventGO.Application.Authorization;
using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

/// <summary>
/// Tạo/sửa sự kiện và các bước đổi trạng thái do tổ chức thực hiện
/// (gửi duyệt, xuất bản, hủy). Duyệt/từ chối nằm ở EventReviewService.
/// </summary>
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

    public async Task<EventResult<ManagedEventResponse>> CreateAndSubmitAsync(
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request, request.TicketTypes ?? []);
        if (request.TicketTypes is not { Count: > 0 })
        {
            validationErrors.Add("Sự kiện phải có ít nhất một loại vé.");
        }

        if (validationErrors.Count > 0)
        {
            return new(null, EventError.Invalid, validationErrors);
        }

        if (!await CategoryIsActiveAsync(request.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        Guid organizationId;
        if (request.OrganizationId is { } requestedOrganizationId)
        {
            var accessError = await CheckOrganizationAccessAsync(
                requestedOrganizationId,
                currentUserId,
                cancellationToken);
            if (accessError != EventError.None)
            {
                return new(null, accessError);
            }

            organizationId = requestedOrganizationId;
        }
        else
        {
            var personalOrganizationId = await GetOrCreatePersonalOrganizationAsync(
                currentUserId,
                request,
                cancellationToken);

            if (personalOrganizationId is null)
            {
                // Tổ chức cá nhân đã bị khóa.
                return new(null, EventError.Forbidden);
            }

            organizationId = personalOrganizationId.Value;
        }

        // Tạo nháp rồi gửi duyệt ngay trong cùng một lần lưu:
        // lịch sử ghi bước Draft → PendingApproval.
        var now = _timeProvider.GetUtcNow();
        var evt = NewEvent(organizationId, currentUserId, request, now);
        evt.Status = EventStatus.PendingApproval;
        _dbContext.Events.Add(evt);
        AddTicketTypes(evt.Id, request.TicketTypes!);
        AddHistory(evt, EventStatus.Draft, currentUserId, "Gửi duyệt sự kiện mới.", now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadManagedAsync(evt.Id, cancellationToken);
        await _notifier.NotifyAdminsAsync(
            ToNotification(response, isResubmission: false),
            cancellationToken);

        return new(response, EventError.None);
    }

    public async Task<EventResult<ManagedEventResponse>> CreateDraftAsync(
        Guid organizationId,
        Guid currentUserId,
        EventDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await CheckOrganizationAccessAsync(
            organizationId,
            currentUserId,
            cancellationToken);
        if (accessError != EventError.None)
        {
            return new(null, accessError);
        }

        var validationErrors = Validate(request, request.TicketTypes ?? []);
        if (validationErrors.Count > 0)
        {
            return new(null, EventError.Invalid, validationErrors);
        }

        if (!await CategoryIsActiveAsync(request.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        var now = _timeProvider.GetUtcNow();
        var evt = NewEvent(organizationId, currentUserId, request, now);
        _dbContext.Events.Add(evt);
        AddTicketTypes(evt.Id, request.TicketTypes ?? []);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(await LoadManagedAsync(evt.Id, cancellationToken), EventError.None);
    }

    public async Task<EventResult<ManagedEventResponse>> UpdateAsync(
        Guid eventId,
        Guid currentUserId,
        EventDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await CheckEventAccessAsync(eventId, currentUserId, cancellationToken);
        if (accessError != EventError.None)
        {
            return new(null, accessError);
        }

        var evt = await _dbContext.Events
            .SingleAsync(x => x.Id == eventId, cancellationToken);

        // Đang chờ duyệt hoặc đã duyệt/công khai: không sửa tự do được.
        if (!EventStatusTransitions.IsEditable(evt.Status))
        {
            return new(null, EventError.InvalidStatus);
        }

        // Không gửi loại vé: các loại vé hiện có vẫn phải hợp lệ với thời gian mới.
        var ticketTypesToCheck = request.TicketTypes
            ?? await _dbContext.TicketTypes
                .AsNoTracking()
                .Where(x => x.EventId == eventId && x.IsActive)
                .Select(x => new TicketTypeRequest
                {
                    Name = x.Name,
                    Price = x.Price,
                    TotalQuantity = x.TotalQuantity,
                    MaxQuantityPerOrder = x.MaxQuantityPerOrder,
                    SaleStartsAt = x.SaleStartsAt,
                    SaleEndsAt = x.SaleEndsAt
                })
                .ToListAsync(cancellationToken);

        var validationErrors = Validate(request, ticketTypesToCheck);
        if (validationErrors.Count > 0)
        {
            return new(null, EventError.Invalid, validationErrors);
        }

        if (!await CategoryIsActiveAsync(request.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            evt.CategoryId = request.CategoryId;
            evt.UpdatedAt = _timeProvider.GetUtcNow();
            ApplyFields(evt, request);

            if (request.TicketTypes is not null)
            {
                // Draft/Rejected chưa từng công khai nên chưa có giữ chỗ/đơn hàng:
                // thay toàn bộ loại vé. Xóa trước, lưu, rồi thêm mới để không
                // vướng unique index (EventId, Name).
                var oldTicketTypes = await _dbContext.TicketTypes
                    .Where(x => x.EventId == evt.Id)
                    .ToListAsync(cancellationToken);
                _dbContext.TicketTypes.RemoveRange(oldTicketTypes);
                await _dbContext.SaveChangesAsync(cancellationToken);

                AddTicketTypes(evt.Id, request.TicketTypes);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Sự kiện vừa được gửi duyệt/đổi trạng thái trong lúc đang sửa.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return new(null, EventError.Conflict);
        }

        return new(await LoadManagedAsync(evt.Id, cancellationToken), EventError.None);
    }

    public async Task<EventResult<ManagedEventResponse>> SubmitAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var (evt, error) = await LoadForTransitionAsync(
            eventId,
            currentUserId,
            EventStatus.PendingApproval,
            cancellationToken);
        if (evt is null)
        {
            return new(null, error);
        }

        var now = _timeProvider.GetUtcNow();
        var errors = new List<string>();
        if (evt.StartsAt <= now)
        {
            errors.Add("Thời gian bắt đầu phải ở tương lai.");
        }

        if (!await HasActiveTicketTypeAsync(evt.Id, cancellationToken))
        {
            errors.Add("Sự kiện phải có ít nhất một loại vé.");
        }

        if (errors.Count > 0)
        {
            return new(null, EventError.Invalid, errors);
        }

        // Thể loại có thể bị Admin tắt sau khi tạo nháp.
        if (!await CategoryIsActiveAsync(evt.CategoryId, cancellationToken))
        {
            return new(null, EventError.CategoryNotFound);
        }

        var isResubmission = evt.Status == EventStatus.Rejected;
        var result = await ChangeStatusAsync(
            evt,
            EventStatus.PendingApproval,
            currentUserId,
            isResubmission ? "Gửi duyệt lại sau khi chỉnh sửa." : "Gửi duyệt sự kiện.",
            now,
            cancellationToken);

        if (result.Data is not null)
        {
            await _notifier.NotifyAdminsAsync(
                ToNotification(result.Data, isResubmission),
                cancellationToken);
        }

        return result;
    }

    public async Task<EventResult<ManagedEventResponse>> PublishAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var (evt, error) = await LoadForTransitionAsync(
            eventId,
            currentUserId,
            EventStatus.Published,
            cancellationToken);
        if (evt is null)
        {
            return new(null, error);
        }

        var now = _timeProvider.GetUtcNow();
        var errors = new List<string>();
        if (evt.EndsAt <= now)
        {
            errors.Add("Sự kiện đã kết thúc, không thể xuất bản.");
        }

        // Loại vé có thể bị tắt qua API ticket-types sau khi được duyệt.
        if (!await HasActiveTicketTypeAsync(evt.Id, cancellationToken))
        {
            errors.Add("Sự kiện phải có ít nhất một loại vé đang mở.");
        }

        if (errors.Count > 0)
        {
            return new(null, EventError.Invalid, errors);
        }

        evt.PublishedAt = now;
        return await ChangeStatusAsync(
            evt,
            EventStatus.Published,
            currentUserId,
            "Xuất bản sự kiện.",
            now,
            cancellationToken);
    }

    public async Task<EventResult<ManagedEventResponse>> CancelAsync(
        Guid eventId,
        Guid currentUserId,
        CancelEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var (evt, error) = await LoadForTransitionAsync(
            eventId,
            currentUserId,
            EventStatus.Cancelled,
            cancellationToken);
        if (evt is null)
        {
            return new(null, error);
        }

        // Chưa có chính sách hoàn tiền: không hủy khi đã có vé giữ chỗ/bán.
        var hasCommittedTickets = await _dbContext.TicketTypes.AnyAsync(
            x => x.EventId == evt.Id && (x.SoldQuantity > 0 || x.ReservedQuantity > 0),
            cancellationToken);
        if (hasCommittedTickets)
        {
            return new(null, EventError.TicketsSold);
        }

        return await ChangeStatusAsync(
            evt,
            EventStatus.Cancelled,
            currentUserId,
            request.Reason?.Trim() is { Length: > 0 } reason ? reason : "Hủy sự kiện.",
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    /// <summary>
    /// Kiểm tra quyền Owner/Manager trên tổ chức sở hữu sự kiện và luồng trạng thái.
    /// </summary>
    private async Task<(Event? Event, EventError Error)> LoadForTransitionAsync(
        Guid eventId,
        Guid currentUserId,
        EventStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var accessError = await CheckEventAccessAsync(eventId, currentUserId, cancellationToken);
        if (accessError != EventError.None)
        {
            return (null, accessError);
        }

        var evt = await _dbContext.Events
            .SingleAsync(x => x.Id == eventId, cancellationToken);

        return EventStatusTransitions.CanTransition(evt.Status, targetStatus)
            ? (evt, EventError.None)
            : (null, EventError.InvalidStatus);
    }

    private async Task<EventResult<ManagedEventResponse>> ChangeStatusAsync(
        Event evt,
        EventStatus targetStatus,
        Guid currentUserId,
        string note,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fromStatus = evt.Status;
        evt.Status = targetStatus;
        evt.UpdatedAt = now;
        AddHistory(evt, fromStatus, currentUserId, note, now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion: người khác vừa đổi trạng thái hoặc sửa sự kiện.
            _dbContext.ChangeTracker.Clear();
            return new(null, EventError.Conflict);
        }

        return new(await LoadManagedAsync(evt.Id, cancellationToken), EventError.None);
    }

    private async Task<EventError> CheckOrganizationAccessAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var membership = await _authorizer.GetMembershipAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        return ToAccessError(membership);
    }

    private async Task<EventError> CheckEventAccessAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var membership = await _authorizer.GetMembershipForEventAsync(
            eventId,
            currentUserId,
            cancellationToken);

        return ToAccessError(membership);
    }

    // Không thuộc tổ chức → 404 (không lộ sự tồn tại); Staff → 403.
    private static EventError ToAccessError(OrganizationMembership? membership) =>
        membership is null
            ? EventError.NotFound
            : OrganizationPermissions.CanManageEvents(membership.Role)
                ? EventError.None
                : EventError.Forbidden;

    /// <summary>
    /// Tổ chức cá nhân của người dùng, tự tạo ở lần đầu.
    /// Trả null nếu tổ chức cá nhân đã bị khóa.
    /// </summary>
    private async Task<Guid?> GetOrCreatePersonalOrganizationAsync(
        Guid userId,
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await FindPersonalOrganizationAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing.Value.IsActive ? existing.Value.Id : null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == userId, cancellationToken);

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
            var created = await FindPersonalOrganizationAsync(userId, cancellationToken);
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

    private List<string> Validate(
        EventDetailsRequest request,
        IReadOnlyCollection<TicketTypeRequest> ticketTypes)
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

        var duplicateNames = ticketTypes
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            errors.Add($"Tên loại vé bị trùng: {string.Join(", ", duplicateNames)}.");
        }

        foreach (var ticketType in ticketTypes)
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

    private Task<bool> CategoryIsActiveAsync(Guid categoryId, CancellationToken cancellationToken) =>
        _dbContext.EventCategories.AnyAsync(
            x => x.Id == categoryId && x.IsActive,
            cancellationToken);

    private Task<bool> HasActiveTicketTypeAsync(Guid eventId, CancellationToken cancellationToken) =>
        _dbContext.TicketTypes.AnyAsync(
            x => x.EventId == eventId && x.IsActive,
            cancellationToken);

    private static Event NewEvent(
        Guid organizationId,
        Guid currentUserId,
        EventDetailsRequest request,
        DateTimeOffset now)
    {
        var evt = new Event
        {
            OrganizationId = organizationId,
            CategoryId = request.CategoryId,
            CreatedByUserId = currentUserId,
            Slug = SlugGenerator.Create(request.Title),
            Status = EventStatus.Draft,
            CreatedAt = now
        };
        ApplyFields(evt, request);
        return evt;
    }

    private static void ApplyFields(Event evt, EventDetailsRequest request)
    {
        evt.Title = request.Title.Trim();
        evt.Description = request.Description.Trim();
        evt.VenueName = request.VenueName.Trim();
        evt.Address = request.Address.Trim();
        evt.City = request.City.Trim();
        evt.StartsAt = request.StartsAt;
        evt.EndsAt = request.EndsAt;
    }

    private void AddHistory(
        Event evt,
        EventStatus fromStatus,
        Guid currentUserId,
        string note,
        DateTimeOffset now) =>
        _dbContext.EventApprovalHistories.Add(new EventApprovalHistory
        {
            EventId = evt.Id,
            FromStatus = fromStatus,
            ToStatus = evt.Status,
            ActionByUserId = currentUserId,
            Note = note,
            CreatedAt = now
        });

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
