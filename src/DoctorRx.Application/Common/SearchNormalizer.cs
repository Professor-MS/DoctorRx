using System.Text;
using System.Text.RegularExpressions;

namespace DoctorRx.Application.Common;

public static class SearchNormalizer
{
    private static readonly Regex MultipleWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes text for Urdu and English clinical search:
    /// - Lowercase & Unicode NFC
    /// - Collapses whitespace
    /// - Strips Arabic diacritics/harakat & tatweel
    /// - Removes zero-width joiners/non-joiners
    /// - Normalizes Arabic letter variants to Urdu forms (ي/ى -> ی, ك -> ک)
    /// - Converts Eastern/Persian/Urdu digits to ASCII (0-9)
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);

        foreach (char c in input)
        {
            // 1. Remove Zero-Width characters (ZWNJ, ZWJ, BOM, etc.)
            if (c is '\u200B' or '\u200C' or '\u200D' or '\uFEFF' or '\u200E' or '\u200F')
            {
                continue;
            }

            // 2. Strip Tatweel / Kashida
            if (c == '\u0640')
            {
                continue;
            }

            // 3. Strip Arabic Harakat / Diacritics (Fatha, Damma, Kasra, Shadda, Sukun, Tanwin, Superscript Alef)
            if (c is >= '\u064B' and <= '\u065F' or '\u0670')
            {
                continue;
            }

            // 4. Normalize Arabic-Indic (٠-٩) and Extended Arabic-Indic (۰-۹) digits to ASCII 0-9
            if (c is >= '\u0660' and <= '\u0669')
            {
                sb.Append((char)('0' + (c - '\u0660')));
                continue;
            }
            if (c is >= '\u06F0' and <= '\u06F9')
            {
                sb.Append((char)('0' + (c - '\u06F0')));
                continue;
            }

            // 5. Normalize Arabic letter variants to Urdu forms
            // ي (Arabic Yeh U+064A) / ى (Alef Maksura U+0649) -> ی (Urdu Yeh U+06CC)
            if (c is '\u064A' or '\u0649')
            {
                sb.Append('\u06CC');
                continue;
            }

            // ك (Arabic Kaf U+0643) -> ک (Urdu Keheh U+06A9)
            if (c == '\u0643')
            {
                sb.Append('\u06A9');
                continue;
            }

            // ة (Teh Marbuta U+0629) -> ہ (Heh U+06C1 / U+0647)
            if (c == '\u0629')
            {
                sb.Append('\u06C1');
                continue;
            }

            // Default: preserve character
            sb.Append(c);
        }

        var cleaned = sb.ToString().ToLowerInvariant();
        cleaned = MultipleWhitespaceRegex.Replace(cleaned, " ").Trim();
        return cleaned.Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Extracts only numeric digits from a phone string, converting Eastern digits first.
    /// </summary>
    public static string NormalizePhoneDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var normalized = Normalize(phone);
        var sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (char.IsAsciiDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
