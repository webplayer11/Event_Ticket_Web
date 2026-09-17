namespace EventGO.Domain.Enums;

public enum ReservationStatus
{
    Active = 0,    // Đang giữ vé
    Converted = 1, // Đã chuyển thành vé bán sau thanh toán
    Released = 2,  // Đã chủ động trả lại số lượng giữ
    Expired = 3    // Đã hết thời hạn giữ
}