using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EventGO.Infrastructure.Events;

public static class SlugGenerator
{
    private const string SuffixChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// "Đêm nhạc Hà Nội" → "dem-nhac-ha-noi-x7k2p9".
    /// Hậu tố ngẫu nhiên giúp slug không trùng giữa các sự kiện cùng tên.
    /// </summary>
    public static string Create(string title, int maxLength = 300)
    {
        var baseSlug = Slugify(title);
        var suffix = RandomSuffix(6);
        var maxBase = maxLength - suffix.Length - 1;

        if (baseSlug.Length > maxBase)
        {
            baseSlug = baseSlug[..maxBase].TrimEnd('-');
        }

        return baseSlug.Length == 0 ? suffix : $"{baseSlug}-{suffix}";
    }

    public static string Slugify(string value)
    {
        // "đ" không tách dấu được bằng Normalize nên thay riêng.
        var normalized = value
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        var previousDash = true;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private static string RandomSuffix(int length)
    {
        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = SuffixChars[RandomNumberGenerator.GetInt32(SuffixChars.Length)];
        }

        return new string(buffer);
    }
}
