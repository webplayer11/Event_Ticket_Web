using System.Security.Cryptography;

namespace EventGO.Infrastructure.Tickets;

public static class TicketCodes
{
    // Crockford base32: bỏ I, L, O, U để đọc/nhập tay mã vé in giấy không nhầm.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Sinh mã dạng TKT-XXXX-XXXX-XXXX (18 ký tự, vừa cột varchar(20)).
    /// 12 ký tự ngẫu nhiên (60 bit) nên trùng mã gần như không xảy ra;
    /// unique index IX_Tickets_TicketCode vẫn là chốt chặn cuối.
    /// </summary>
    public static string Generate()
    {
        var random = RandomNumberGenerator.GetString(Alphabet, 12);
        return $"TKT-{random[..4]}-{random[4..8]}-{random[8..]}";
    }
}
