namespace EventGO.Infrastructure.Authentication;

public sealed class PasswordResetEmailOptions
{
    public const string SectionName = "PasswordResetEmail";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "EventGO";

    public string ResetUrl { get; set; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(FromAddress)
        && Uri.TryCreate(ResetUrl, UriKind.Absolute, out _);
}
