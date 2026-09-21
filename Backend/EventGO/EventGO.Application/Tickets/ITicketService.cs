using EventGO.Application.Tickets.Dtos;

namespace EventGO.Application.Tickets;

public interface ITicketService
{
    /// <summary>
    /// Phát hành vé cho một đơn hàng đã thanh toán.
    /// Idempotent: gọi lại không tạo vé trùng.
    /// </summary>
    Task<List<TicketResponse>> IssueTicketsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách vé thuộc về user hiện tại.
    /// Query theo ownership: Ticket → OrderItem → Order → User.
    /// </summary>
    Task<List<TicketResponse>> GetMyTicketsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu QR token để xác minh vé (dành cho check-in).
    /// Nhận raw token → hash SHA-256 → lookup QrTokenHash.
    /// </summary>
    Task<TicketValidationResponse?> ValidateQrTokenAsync(
        string qrToken,
        CancellationToken cancellationToken = default);
}
