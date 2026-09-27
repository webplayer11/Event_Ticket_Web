using System.Net;
using System.Net.Mail;
using EventGO.Application.Authentication;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Authentication;

public sealed class SmtpPasswordResetEmailSender
    : IPasswordResetEmailSender
{
    private readonly PasswordResetEmailOptions _options;

    public SmtpPasswordResetEmailSender(
        IOptions<PasswordResetEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured())
        {
            throw new InvalidOperationException(
                "Password reset email delivery is not configured.");
        }

        var separator = _options.ResetUrl.Contains('?') ? '&' : '?';
        var resetLink = string.Concat(
            _options.ResetUrl,
            separator,
            "email=",
            Uri.EscapeDataString(recipientEmail),
            "&token=",
            Uri.EscapeDataString(resetToken));

        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromAddress,
                _options.FromName),
            Subject = "Đặt lại mật khẩu EventGO",
            Body = string.Concat(
                "Bạn đã yêu cầu đặt lại mật khẩu EventGO.",
                Environment.NewLine,
                "Mở liên kết sau để tiếp tục:",
                Environment.NewLine,
                resetLink,
                Environment.NewLine,
                "Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email."),
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(
                _options.UserName,
                _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
