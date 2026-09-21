using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Authentication.Dtos;

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150, MinimumLength = 2,
        ErrorMessage = "Họ tên phải có từ 2 đến 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}
