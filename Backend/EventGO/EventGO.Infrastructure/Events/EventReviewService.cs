using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

public sealed class EventReviewService : IEventReviewService
{
    private readonly EventGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EventReviewService(EventGoDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<List<ManagedEventResponse>> GetForReviewAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var filter = Enum.TryParse<EventStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : EventStatus.PendingApproval;

        var events = await EventReadModel.LoadAsync(
            _dbContext,
            _dbContext.Events.Where(x => x.Status == filter),
            cancellationToken);

        // Hàng chờ duyệt: sự kiện gửi trước được xử lý trước.
        return filter == EventStatus.PendingApproval
            ? events.OrderBy(x => x.CreatedAt).ToList()
            : events.OrderByDescending(x => x.LastReviewedAt ?? x.CreatedAt).ToList();
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Events.CountAsync(
            x => x.Status == EventStatus.PendingApproval,
            cancellationToken);

    public async Task<EventResult<ManagedEventResponse>> ApproveAsync(
        Guid eventId,
        Guid adminUserId,
        ReviewEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var evt = await _dbContext.Events
            .SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);

        if (evt is null)
        {
            return new(null, EventError.NotFound);
        }

        if (evt.Status != EventStatus.PendingApproval)
        {
            return new(null, EventError.InvalidStatus);
        }

        var now = _timeProvider.GetUtcNow();
        if (evt.EndsAt <= now)
        {
            return new(null, EventError.Invalid, ["Sự kiện đã kết thúc, không thể duyệt."]);
        }

        evt.Status = EventStatus.Published;
        evt.PublishedAt = now;
        evt.UpdatedAt = now;

        return await SaveReviewAsync(
            evt,
            adminUserId,
            EventStatus.PendingApproval,
            request.Note?.Trim() is { Length: > 0 } note ? note : "Đã duyệt.",
            now,
            cancellationToken);
    }

    public async Task<EventResult<ManagedEventResponse>> RejectAsync(
        Guid eventId,
        Guid adminUserId,
        ReviewEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Note?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return new(null, EventError.Invalid, ["Vui lòng nhập lý do từ chối."]);
        }

        var evt = await _dbContext.Events
            .SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);

        if (evt is null)
        {
            return new(null, EventError.NotFound);
        }

        if (evt.Status != EventStatus.PendingApproval)
        {
            return new(null, EventError.InvalidStatus);
        }

        var now = _timeProvider.GetUtcNow();
        evt.Status = EventStatus.Rejected;
        evt.UpdatedAt = now;

        return await SaveReviewAsync(
            evt,
            adminUserId,
            EventStatus.PendingApproval,
            reason,
            now,
            cancellationToken);
    }

    private async Task<EventResult<ManagedEventResponse>> SaveReviewAsync(
        Event evt,
        Guid adminUserId,
        EventStatus fromStatus,
        string note,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _dbContext.EventApprovalHistories.Add(new EventApprovalHistory
        {
            EventId = evt.Id,
            FromStatus = fromStatus,
            ToStatus = evt.Status,
            ActionByUserId = adminUserId,
            Note = note,
            CreatedAt = now
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion: Admin khác vừa duyệt/từ chối, hoặc người tạo vừa sửa.
            _dbContext.ChangeTracker.Clear();
            return new(null, EventError.Conflict);
        }

        var response = (await EventReadModel.LoadAsync(
            _dbContext,
            _dbContext.Events.Where(x => x.Id == evt.Id),
            cancellationToken)).Single();

        return new(response, EventError.None);
    }
}
