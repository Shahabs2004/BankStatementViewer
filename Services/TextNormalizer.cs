using System.Text;
using System.Text.RegularExpressions;

namespace BankStatementViewer.Services;

/// <summary>Normalizes Persian/Arabic spelling differences and digits before comparing bank text.</summary>
public static class TextNormalizer
{
    public static string Normalize(string? value, bool removeWhitespace = false)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var normalized = value.Normalize(NormalizationForm.FormKC)
            .Replace('ي', 'ی').Replace('ى', 'ی').Replace('ك', 'ک')
            .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ٱ', 'ا')
            .Replace('ۀ', 'ه').Replace('ة', 'ه').Replace('\u0640', ' ')
            .Replace('\u200c', ' ').Replace('\u200d', ' ')
            .Replace('\u200e', ' ').Replace('\u200f', ' ')
            .Replace('\u202a', ' ').Replace('\u202b', ' ').Replace('\u202c', ' ')
            .Replace('\u00a0', ' ');

        // Ignore Arabic combining marks/diacritics so visually identical text still matches.
        normalized = Regex.Replace(normalized, @"\p{Mn}+", string.Empty);
        var chars = normalized.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + chars[i] - '۰');
            else if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + chars[i] - '٠');
        }
        normalized = new string(chars).ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return removeWhitespace ? normalized.Replace(" ", string.Empty, StringComparison.Ordinal) : normalized;
    }

    public static bool Contains(string? source, string? search)
    {
        var normalizedSource = Normalize(source);
        var normalizedSearch = Normalize(search);
        if (normalizedSearch.Length == 0) return true;
        if (normalizedSource.Contains(normalizedSearch, StringComparison.Ordinal)) return true;

        // Some bank systems insert spaces or line breaks inside otherwise identical tokens.
        // Work from the already-normalized strings to avoid repeating Unicode normalization.
        var compactSource = normalizedSource.Replace(" ", string.Empty, StringComparison.Ordinal);
        var compactSearch = normalizedSearch.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compactSource.Contains(compactSearch, StringComparison.Ordinal);
    }

    public static bool EqualsNormalized(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal)) return true;
        return string.Equals(
            normalizedLeft.Replace(" ", string.Empty, StringComparison.Ordinal),
            normalizedRight.Replace(" ", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }
}
