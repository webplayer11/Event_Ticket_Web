namespace EventGO.Application.Tickets.Dtos;

/// <summary>
/// Kết quả tra cứu QR token — cung cấp đủ thông tin
/// để staff quyết định check-in hay từ chối.
/// </summary>
public class TicketValidationResponse
{
    public Guid TicketId { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public string EventTitle { get; set; } = string.Empty;

    public string TicketTypeName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsCheckedIn { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;
}
