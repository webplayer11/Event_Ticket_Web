using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Tickets;

/// <summary>
/// QR token = HMAC-SHA256(khóa server, ticketId), không suy ra được từ
/// TicketCode in trên vé. DB chỉ lưu SHA256 của token; server tính lại
/// được token từ ticketId để hiển thị QR cho chủ vé.
/// </summary>
public sealed class TicketQrTokens
{
    private const string Purpose = "eventgo-ticket-qr:v1:";

    private readonly byte[] _signingKey;

    public TicketQrTokens(IOptions<TicketQrOptions> options)
    {
        _signingKey = Convert.FromBase64String(options.Value.SigningKey);
    }

    public string CreateToken(Guid ticketId)
    {
        var mac = HMACSHA256.HashData(
            _signingKey,
            Encoding.UTF8.GetBytes(Purpose + ticketId.ToString("N")));

        return Convert.ToBase64String(mac)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
