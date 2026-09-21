namespace EventGO.Domain.Enums;

public enum EventStatus
{
    Draft = 0,           // Bản nháp
    PendingApproval = 1, // Đang chờ duyệt
    Approved = 2,        // Đã duyệt, chưa công khai
    Rejected = 3,        // Bị từ chối
    Published = 4,       // Đã công khai
    Cancelled = 5,       // Đã hủy
    Completed = 6        // Đã kết thúc
}