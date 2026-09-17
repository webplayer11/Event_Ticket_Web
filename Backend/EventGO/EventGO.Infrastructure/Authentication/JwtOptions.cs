namespace EventGO.Infrastructure.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    // Khóa ngẫu nhiên được lưu dưới dạng Base64.
    public string SecretKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public bool HasValidSecretKey()
    {
        try
        {
            return Convert.FromBase64String(SecretKey).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}