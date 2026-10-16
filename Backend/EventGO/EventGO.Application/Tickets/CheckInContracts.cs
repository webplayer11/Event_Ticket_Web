using System.ComponentModel.DataAnnotations;
using EventGO.Application.Tickets.Dtos;

namespace EventGO.Application.Tickets;

public enum TicketCheckInError
{
    None,
    // Không có phân công soát vé còn hiệu lực cho sự kiện.
    Forbidden,
    // QR không tồn tại hoặc thuộc sự kiện khác.
    TicketNotFound,
    // Vé đã bị hủy.
    TicketNotValid,
    AlreadyCheckedIn
}

public sealed record TicketCheckInResult(
    TicketValidationResponse? Ticket,
    TicketCheckInError Error);

public class ValidateTicketRequest
{
    [Required]
    [StringLength(200)]
    public string QrToken { get; set; } = string.Empty;
}

public class CheckInTicketRequest
{
    [Required]
    [StringLength(200)]
    public string QrToken { get; set; } = string.Empty;

    [StringLength(100)]
    public string? GateName { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}
