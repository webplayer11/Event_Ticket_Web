using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Authentication.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}