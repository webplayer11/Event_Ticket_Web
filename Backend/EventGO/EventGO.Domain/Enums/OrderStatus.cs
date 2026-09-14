namespace EventGO.Domain.Enums;

public enum OrderStatus
{
    PendingPayment = 0, // Chờ thanh toán
    Paid = 1,           // Đã thanh toán
    Cancelled = 2,      // Đã hủy
    Expired = 3,        // Hết thời hạn thanh toán
    Refunded = 4        // Đã hoàn tiền toàn bộ
}