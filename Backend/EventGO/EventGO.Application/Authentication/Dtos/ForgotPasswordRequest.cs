using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Authentication.Dtos;

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
