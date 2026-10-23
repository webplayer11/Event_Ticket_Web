using EventGO.Application.Authorization;
using EventGO.Application.Tickets;
using EventGO.Application.Tickets.Dtos;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Tickets;

public class TicketService : ITicketService
{
    private readonly EventGoDbContext _dbContext;
    private readonly IOrganizationAuthorizer _authorizer;
    private readonly TimeProvider _timeProvider;

    public TicketService(
        EventGoDbContext dbContext,
        IOrganizationAuthorizer authorizer,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
        _timeProvider = timeProvider;
    }

    // ───────────────────────────────────────────────
    //  "Vé của tôi"
    // ───────────────────────────────────────────────

    public async Task<List<TicketResponse>> GetMyTicketsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tickets = await (
            from ticket in _dbContext.Tickets.AsNoTracking()
            join orderItem in _dbContext.OrderItems.AsNoTracking()
                on ticket.OrderItemId equals orderItem.Id
            join order in _dbContext.Orders.AsNoTracking()
                on orderItem.OrderId equals order.Id
            join evt in _dbContext.Events.AsNoTracking()
                on order.EventId equals evt.Id
            where order.UserId == userId
                && order.Status == OrderStatus.Paid
            orderby ticket.IssuedAt descending
            select new TicketResponse
            {
                Id = ticket.Id,
                TicketCode = ticket.TicketCode,
                EventTitle = evt.Title,
                EventStartsAt = evt.StartsAt,
                VenueName = evt.VenueName,
                TicketTypeName = orderItem.TicketTypeName,
                Status = ticket.Status.ToString(),
                IssuedAt = ticket.IssuedAt,
                QrToken = null // Không trả raw token trong danh sách
            }
        ).ToListAsync(cancellationToken);

        return tickets;
    }

    // ───────────────────────────────────────────────
    //  Validate QR Token + Check-in
    // ───────────────────────────────────────────────

    public async Task<TicketCheckInResult> ValidateQrTokenAsync(
        Guid staffUserId,
        Guid eventId,
        string qrToken,
        CancellationToken cancellationToken = default)
    {
        var assignmentId = await _authorizer.GetActiveStaffAssignmentIdAsync(
            eventId,
            staffUserId,
            cancellationToken);

        if (assignmentId is null)
        {
            return new(null, TicketCheckInError.Forbidden);
        }

        var lookup = await FindTicketForEventAsync(
            eventId,
            qrToken,
            cancellationToken);

        if (lookup is null)
        {
            return new(null, TicketCheckInError.TicketNotFound);
        }

        return lookup.IsUsable
            ? new(lookup.Ticket, TicketCheckInError.None)
            : new(lookup.Ticket, TicketCheckInError.TicketNotValid);
    }

    public async Task<TicketCheckInResult> CheckInAsync(
        Guid staffUserId,
        Guid eventId,
        CheckInTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var assignmentId = await _authorizer.GetActiveStaffAssignmentIdAsync(
            eventId,
            staffUserId,
            cancellationToken);

        if (assignmentId is null)
        {
            return new(null, TicketCheckInError.Forbidden);
        }

        var lookup = await FindTicketForEventAsync(
            eventId,
            request.QrToken,
            cancellationToken);

        if (lookup is null)
        {
            return new(null, TicketCheckInError.TicketNotFound);
        }

        var found = lookup.Ticket;

        if (found.IsCheckedIn || found.Status == TicketStatus.Used.ToString())
        {
            return new(found, TicketCheckInError.AlreadyCheckedIn);
        }

        if (!lookup.IsUsable)
        {
            return new(found, TicketCheckInError.TicketNotValid);
        }

        var ticket = await _dbContext.Tickets
            .SingleAsync(x => x.Id == found.TicketId, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        ticket.Status = TicketStatus.Used;
        ticket.UsedAt = now;

        _dbContext.CheckIns.Add(new CheckIn
        {
            TicketId = ticket.Id,
            EventStaffAssignmentId = assignmentId.Value,
            CheckedInAt = now,
            GateName = request.GateName?.Trim(),
            Note = request.Note?.Trim()
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception is DbUpdateConcurrencyException
                || exception.IsUniqueViolation())
        {
            // Hai cổng quét cùng một vé: RowVersion của Ticket hoặc
            // unique index CheckIns.TicketId chặn lượt thứ hai.
            // Lỗi DB khác không bị che, để lộ ra thành 500 và được log.
            _dbContext.ChangeTracker.Clear();
            return new(found, TicketCheckInError.AlreadyCheckedIn);
        }

        found.Status = ticket.Status.ToString();
        found.IsCheckedIn = true;
        return new(found, TicketCheckInError.None);
    }

    /// <param name="IsUsable">
    /// Vé còn hiệu lực, đơn hàng vẫn ở trạng thái đã thanh toán
    /// (chưa hoàn tiền/hủy) và sự kiện chưa bị hủy.
    /// </param>
    private sealed record TicketLookup(
        TicketValidationResponse Ticket,
        bool IsUsable);

    private async Task<TicketLookup?> FindTicketForEventAsync(
        Guid eventId,
        string qrToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return null;
        }

        var hash = TicketQrTokens.Hash(qrToken.Trim());

        var result = await (
            from ticket in _dbContext.Tickets.AsNoTracking()
            join orderItem in _dbContext.OrderItems.AsNoTracking()
                on ticket.OrderItemId equals orderItem.Id
            join order in _dbContext.Orders.AsNoTracking()
                on orderItem.OrderId equals order.Id
            join evt in _dbContext.Events.AsNoTracking()
                on order.EventId equals evt.Id
            where ticket.QrTokenHash == hash
                // Vé của sự kiện khác coi như không tồn tại.
                && evt.Id == eventId
            select new
            {
                ticket.Id,
                ticket.TicketCode,
                ticket.Status,
                OrderStatus = order.Status,
                EventStatus = evt.Status,
                EventId = evt.Id,
                evt.Title,
                orderItem.TicketTypeName,
                order.CustomerName,
                order.CustomerEmail
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return null;
        }

        // Kiểm tra đã check-in chưa
        var isCheckedIn = await _dbContext.CheckIns
            .AsNoTracking()
            .AnyAsync(
                c => c.TicketId == result.Id,
                cancellationToken);

        var isUsable = result.Status == TicketStatus.Valid
            && result.OrderStatus == OrderStatus.Paid
            && result.EventStatus != EventStatus.Cancelled;

        var response = new TicketValidationResponse
        {
            TicketId = result.Id,
            TicketCode = result.TicketCode,
            EventId = result.EventId,
            EventTitle = result.Title,
            TicketTypeName = result.TicketTypeName,
            Status = result.Status.ToString(),
            IsCheckedIn = isCheckedIn,
            CustomerName = result.CustomerName,
            // Người soát vé chỉ cần đối chiếu, không cần email đầy đủ.
            CustomerEmail = MaskEmail(result.CustomerEmail)
        };

        return new TicketLookup(response, isUsable);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return string.Empty;
        }

        var visible = Math.Min(2, at);
        return string.Concat(email.AsSpan(0, visible), "***", email.AsSpan(at));
    }
}
