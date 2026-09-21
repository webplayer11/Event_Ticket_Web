using System.Security.Cryptography;
using System.Text;
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

    public TicketService(EventGoDbContext dbContext)
    {
        _dbContext = dbContext;
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
    //  Validate QR Token (cho check-in)
    // ───────────────────────────────────────────────

    public async Task<TicketValidationResponse?> ValidateQrTokenAsync(
        string qrToken,
        CancellationToken cancellationToken = default)
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
            select new
            {
                ticket.Id,
                ticket.TicketCode,
                ticket.Status,
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

        return new TicketValidationResponse
        {
            TicketId = result.Id,
            TicketCode = result.TicketCode,
            EventId = result.EventId,
            EventTitle = result.Title,
            TicketTypeName = result.TicketTypeName,
            Status = result.Status.ToString(),
            IsCheckedIn = isCheckedIn,
            CustomerName = result.CustomerName,
            CustomerEmail = result.CustomerEmail
        };
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
