namespace EventGO.Application.Authentication;

public interface IPasswordResetEmailSender
{
    Task SendAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default);
}
