using System.Net;
using System.Net.Mail;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using EventGO.Infrastructure.Authentication;
using EventGO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Events;

public sealed class EventApprovalOptions
{
    public const string SectionName = "EventApproval";

    // Trang duyệt sự kiện của Admin trên frontend (không bắt buộc),
    // vd: http://localhost:5173/admin/events. Có thì chèn link vào email.
    public string AdminReviewUrl { get; set; } = string.Empty;
}

/// <summary>
/// Gửi một email (BCC) tới mọi PlatformAdmin đang hoạt động khi có sự kiện
/// chờ duyệt. Dùng chung cấu hình SMTP của PasswordResetEmail.
/// Chưa cấu hình SMTP hoặc gửi lỗi chỉ ghi log: sự kiện vẫn nằm trong
/// danh sách chờ duyệt của Admin.
/// </summary>
public sealed class SmtpEventApprovalNotifier : IEventApprovalNotifier
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PasswordResetEmailOptions _smtp;
    private readonly EventApprovalOptions _options;
    private readonly ILogger<SmtpEventApprovalNotifier> _logger;

    public SmtpEventApprovalNotifier(
        UserManager<ApplicationUser> userManager,
        IOptions<PasswordResetEmailOptions> smtp,
        IOptions<EventApprovalOptions> options,
        ILogger<SmtpEventApprovalNotifier> logger)
    {
        _userManager = userManager;
        _smtp = smtp.Value;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAdminsAsync(
        EventSubmittedNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (!_smtp.IsSmtpConfigured())
        {
            _logger.LogInformation(
                "Chưa cấu hình SMTP: sự kiện {EventId} chờ duyệt chỉ hiển thị " +
                "trong danh sách duyệt của Admin, không gửi email.",
                notification.EventId);
            return;
        }

        try
        {
            var adminEmails = (await _userManager.GetUsersInRoleAsync(
                    SystemRoles.PlatformAdmin))
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Email))
                .Select(x => x.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (adminEmails.Count == 0)
            {
                _logger.LogWarning(
                    "Không có PlatformAdmin nào để báo duyệt sự kiện {EventId}.",
                    notification.EventId);
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromAddress, _smtp.FromName),
                Subject = notification.IsResubmission
                    ? $"[EventGO] Sự kiện gửi duyệt lại: {notification.Title}"
                    : $"[EventGO] Sự kiện mới chờ duyệt: {notification.Title}",
                Body = BuildBody(notification),
                IsBodyHtml = false
            };

            // BCC để các Admin không thấy email của nhau.
            message.To.Add(new MailAddress(_smtp.FromAddress, _smtp.FromName));
            foreach (var email in adminEmails)
            {
                message.Bcc.Add(email);
            }

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl = _smtp.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_smtp.UserName))
            {
                client.Credentials = new NetworkCredential(
                    _smtp.UserName,
                    _smtp.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Không gửi được email báo duyệt sự kiện {EventId}.",
                notification.EventId);
        }
    }

    private string BuildBody(EventSubmittedNotification notification)
    {
        var lines = new List<string>
        {
            notification.IsResubmission
                ? "Một sự kiện bị từ chối trước đó đã được chỉnh sửa và gửi duyệt lại."
                : "Có sự kiện mới đang chờ duyệt.",
            string.Empty,
            $"Tên sự kiện: {notification.Title}",
            $"Ban tổ chức: {notification.OrganizationName}",
            $"Người gửi: {notification.SubmittedByName} ({notification.SubmittedByEmail})",
            $"Bắt đầu: {notification.StartsAt:dd/MM/yyyy HH:mm} (UTC{notification.StartsAt:zzz})",
            $"Mã sự kiện: {notification.EventId}"
        };

        if (Uri.TryCreate(_options.AdminReviewUrl, UriKind.Absolute, out var reviewUrl))
        {
            lines.Add(string.Empty);
            lines.Add($"Duyệt tại: {reviewUrl}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
