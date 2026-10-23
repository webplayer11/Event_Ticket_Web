namespace EventGO.Infrastructure.Tickets;

public class TicketQrOptions
{
    public const string SectionName = "TicketQr";

    // Khóa HMAC ngẫu nhiên dưới dạng Base64, tách riêng khỏi Jwt:SecretKey.
    // Đổi khóa làm mọi QR đã phát hành mất hiệu lực.
    public string SigningKey { get; set; } = string.Empty;

    public bool HasValidSigningKey()
    {
        try
        {
            return Convert.FromBase64String(SigningKey).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
