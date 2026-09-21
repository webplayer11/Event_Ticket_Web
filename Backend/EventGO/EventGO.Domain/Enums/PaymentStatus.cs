namespace EventGO.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,   // Đang chờ kết quả
    Succeeded = 1, // Thanh toán thành công
    Failed = 2,    // Thanh toán thất bại
    Cancelled = 3, // Giao dịch bị hủy
    Refunded = 4   // Đã hoàn tiền toàn bộ
}