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
    /// Chỉ người có phân công soát vé còn hiệu lực cho sự kiện mới được tra cứu,
    /// và chỉ tìm vé thuộc đúng sự kiện đó.
    /// </summary>
    Task<TicketCheckInResult> ValidateQrTokenAsync(
        Guid staffUserId,
        Guid eventId,
        string qrToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check-in vé. Cùng điều kiện quyền như ValidateQrTokenAsync;
    /// mỗi vé chỉ check-in được một lần.
    /// </summary>
    Task<TicketCheckInResult> CheckInAsync(
        Guid staffUserId,
        Guid eventId,
        CheckInTicketRequest request,
        CancellationToken cancellationToken = default);
}
