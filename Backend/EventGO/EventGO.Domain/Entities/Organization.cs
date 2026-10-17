namespace EventGO.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Khác null: tổ chức cá nhân, hệ thống tự tạo khi người dùng
    // tạo sự kiện lần đầu. Mỗi người dùng có tối đa một tổ chức cá nhân.
    public Guid? PersonalOwnerUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}