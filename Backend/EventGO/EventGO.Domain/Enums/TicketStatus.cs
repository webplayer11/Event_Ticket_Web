namespace EventGO.Domain.Enums;

public enum TicketStatus
{
    Valid = 0,   // Vé có hiệu lực
    Used = 1,    // Vé đã check-in
    Cancelled = 2 // Vé đã bị hủy hoặc vô hiệu hóa
}