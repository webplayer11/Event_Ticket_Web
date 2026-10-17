using System.Security.Cryptography;
using System.Text;
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
    //  Phát hành vé sau thanh toán (idempotent)
    // ───────────────────────────────────────────────

    public async Task<List<TicketResponse>> IssueTicketsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // 1. Load Order kèm OrderItems
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(
                o => o.Id == orderId,
                cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy đơn hàng {orderId}.");
        }

        if (order.Status != OrderStatus.Paid)
        {
            throw new InvalidOperationException(
                $"Đơn hàng {orderId} chưa được thanh toán (status = {order.Status}).");
        }

        // 2. Idempotent: nếu đã phát hành vé cho order này thì trả về luôn
        var orderItemIds = order.Items.Select(i => i.Id).ToList();

        var existingTickets = await _dbContext.Tickets
            .AsNoTracking()
            .Where(t => orderItemIds.Contains(t.OrderItemId))
            .ToListAsync(cancellationToken);

        if (existingTickets.Count > 0)
        {
            // Đã phát hành rồi — trả kết quả mà không tạo mới
            return await BuildTicketResponsesAsync(
                existingTickets, order, cancellationToken);
        }

        // 3. Tạo Ticket cho mỗi đơn vị trong mỗi OrderItem
        var newTickets = new List<Ticket>();

        foreach (var item in order.Items)
        {
            for (var i = 0; i < item.Quantity; i++)
            {
                var qrTokenRaw = Guid.NewGuid().ToString();
                var qrTokenHash = HashSha256(qrTokenRaw);

                var ticket = new Ticket
                {
                    OrderItemId = item.Id,
                    TicketCode = GenerateTicketCode(),
                    QrTokenHash = qrTokenHash,
                    Status = TicketStatus.Valid,
                    IssuedAt = DateTimeOffset.UtcNow
                };

                // Lưu raw token tạm để trả về trong response
                // (không persist — chỉ hash được lưu vào DB)
                ticket.GetType()
                    .GetProperty(nameof(Ticket.QrTokenHash))!
                    .SetValue(ticket, qrTokenHash);

                newTickets.Add(ticket);

                // Tag raw token vào một dictionary tạm
                _qrTokenMap[ticket.Id] = qrTokenRaw;
            }
        }

        _dbContext.Tickets.AddRange(newTickets);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildTicketResponsesAsync(
            newTickets, order, cancellationToken);
    }

    // Dictionary tạm giữ raw QR token trong request scope
    private readonly Dictionary<Guid, string> _qrTokenMap = new();

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

        var hash = HashSha256(qrToken.Trim());

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

    // ───────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────

    /// <summary>
    /// Sinh mã vé dạng TKT-{base36 timestamp}-{random}.
    /// Tổng max 20 ký tự, phù hợp in vé giấy.
    /// </summary>
    private static string GenerateTicketCode()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var base36Time = ToBase36(timestamp);
        var random = GenerateRandomAlphanumeric(4);
        var code = $"TKT-{base36Time}-{random}";

        // Đảm bảo không vượt quá 20 ký tự
        if (code.Length > 20)
        {
            code = code[..20];
        }

        return code.ToUpperInvariant();
    }

    private static string ToBase36(long value)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = new StringBuilder();

        while (value > 0)
        {
            result.Insert(0, chars[(int)(value % 36)]);
            value /= 36;
        }

        return result.Length == 0 ? "0" : result.ToString();
    }

    private static string GenerateRandomAlphanumeric(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var buffer = new char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(buffer);
    }

    private static string HashSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Build response list cho các tickets đã tồn tại (idempotent return).
    /// </summary>
    private async Task<List<TicketResponse>> BuildTicketResponsesAsync(
        List<Ticket> tickets,
        Order order,
        CancellationToken cancellationToken)
    {
        // Load event info
        var evt = await _dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Id == order.EventId,
                cancellationToken);

        // Build OrderItem lookup
        var orderItemIds = tickets
            .Select(t => t.OrderItemId)
            .Distinct()
            .ToList();

        var orderItems = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(oi => orderItemIds.Contains(oi.Id))
            .ToDictionaryAsync(
                oi => oi.Id,
                cancellationToken);

        return tickets.Select(t => new TicketResponse
        {
            Id = t.Id,
            TicketCode = t.TicketCode,
            EventTitle = evt?.Title ?? string.Empty,
            EventStartsAt = evt?.StartsAt ?? default,
            VenueName = evt?.VenueName ?? string.Empty,
            TicketTypeName = orderItems.TryGetValue(
                t.OrderItemId, out var oi)
                    ? oi.TicketTypeName
                    : string.Empty,
            Status = t.Status.ToString(),
            IssuedAt = t.IssuedAt,
            QrToken = _qrTokenMap.TryGetValue(t.Id, out var raw)
                ? raw
                : null
        }).ToList();
    }
}
