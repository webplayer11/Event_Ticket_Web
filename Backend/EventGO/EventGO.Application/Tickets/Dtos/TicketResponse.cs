using EventGO.Domain.Enums;

namespace EventGO.Application.Tickets.Dtos;

public class TicketResponse
{
    public Guid Id { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public string EventTitle { get; set; } = string.Empty;

    public DateTimeOffset EventStartsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string TicketTypeName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>
    /// Raw QR token — chỉ gửi cho owner của vé.
    /// Staff dùng endpoint validate thay vì nhận trực tiếp.
    /// </summary>
    public string? QrToken { get; set; }
}
