using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.Common;

public static class SearchNormalizer
{
    private static readonly Regex MultipleWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly char[] WordSeparators = [' ', '\t', '\r', '\n', '-', '_', '/', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}'];

    /// <summary>
    /// Normalizes text for Urdu and English clinical search:
    /// - Lowercase & Unicode NFC
    /// - Collapses whitespace
    /// - Strips Arabic diacritics/harakat & tatweel
    /// - Removes zero-width joiners/non-joiners
    /// - Normalizes Arabic letter variants to Urdu forms (ي/ى -> ی, ك -> ک, ة -> ہ)
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

    /// <summary>
    /// Symmetrically strips leading zeros from a digit string.
    /// E.g. "0333" -> "333", "000012" -> "12", "0" -> "0".
    /// </summary>
    public static string StripLeadingZeros(string? digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        var trimmed = digits.Trim().TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    /// <summary>
    /// Splits normalized text into distinct word tokens.
    /// </summary>
    public static HashSet<string> ExtractWordTokens(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var normalized = Normalize(text);
        var words = normalized.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var word in words)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                tokens.Add(word);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Generates phone suffix tokens for substring matching.
    /// Per Amendment 1: Store all digit-suffix tokens (length >= 3) so any digit substring is a token prefix.
    /// Leading zeros are symmetrically stripped.
    /// </summary>
    public static HashSet<string> GeneratePhoneSuffixTokens(string? phone)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawDigits = NormalizePhoneDigits(phone);
        if (string.IsNullOrWhiteSpace(rawDigits))
        {
            return tokens;
        }

        var cleanDigits = StripLeadingZeros(rawDigits);
        if (cleanDigits.Length < 3)
        {
            if (!string.IsNullOrEmpty(cleanDigits))
            {
                tokens.Add(cleanDigits);
            }
            return tokens;
        }

        // All suffixes of length >= 3
        for (int i = 0; i <= cleanDigits.Length - 3; i++)
        {
            tokens.Add(cleanDigits.Substring(i));
        }

        return tokens;
    }

    private static readonly char[] QuerySeparators = [' ', '\t', '\r', '\n', ',', ';', ':', '|', '(', ')', '[', ']', '{', '}'];

    /// <summary>
    /// Generates record number tokens for a patient record number (e.g. "P-000012").
    /// Accepts P-000012, 000012, and 12.
    /// Per Amendment 4: digit-only query matches the numeric value exactly after stripping leading zeros, plus text prefix "p-".
    /// </summary>
    public static HashSet<string> GenerateRecordNumberTokens(string? recordNumber)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(recordNumber))
        {
            return tokens;
        }

        var normalized = Normalize(recordNumber);
        tokens.Add(normalized); // e.g. "p-000012"

        var digits = NormalizePhoneDigits(recordNumber);
        if (!string.IsNullOrEmpty(digits))
        {
            var stripped = StripLeadingZeros(digits);
            tokens.Add(digits); // e.g. "000012"
            tokens.Add(stripped); // e.g. "12"
            tokens.Add($"p-{stripped}"); // e.g. "p-12"
            tokens.Add($"p-{digits}"); // e.g. "p-000012"
        }

        return tokens;
    }

    /// <summary>
    /// Generates all search tokens for a Patient entity.
    /// </summary>
    public static List<PatientSearchToken> GeneratePatientTokens(Patient patient)
    {
        var result = new List<PatientSearchToken>();

        // 1. Name word tokens
        var nameWords = ExtractWordTokens(patient.Name);
        foreach (var word in nameWords)
        {
            result.Add(new PatientSearchToken
            {
                PatientId = patient.Id,
                Token = word,
                TokenType = SearchTokenType.NameWord
            });
        }

        // 2. Phone suffix tokens
        if (!string.IsNullOrWhiteSpace(patient.Phone))
        {
            var phoneTokens = GeneratePhoneSuffixTokens(patient.Phone);
            foreach (var token in phoneTokens)
            {
                result.Add(new PatientSearchToken
                {
                    PatientId = patient.Id,
                    Token = token,
                    TokenType = SearchTokenType.PhoneSuffix
                });
            }
        }

        // 3. Record number tokens
        if (!string.IsNullOrWhiteSpace(patient.RecordNumber))
        {
            var recordTokens = GenerateRecordNumberTokens(patient.RecordNumber);
            foreach (var token in recordTokens)
            {
                result.Add(new PatientSearchToken
                {
                    PatientId = patient.Id,
                    Token = token,
                    TokenType = SearchTokenType.RecordNumber
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Generates all search tokens for a Medicine entity.
    /// </summary>
    public static List<MedicineSearchToken> GenerateMedicineTokens(Medicine medicine)
    {
        var result = new List<MedicineSearchToken>();

        // Brand name tokens
        var nameWords = ExtractWordTokens(medicine.Name);
        foreach (var word in nameWords)
        {
            result.Add(new MedicineSearchToken
            {
                MedicineId = medicine.Id,
                Token = word,
                TokenType = SearchTokenType.MedicineNameWord
            });
        }

        // Generic name tokens
        if (!string.IsNullOrWhiteSpace(medicine.GenericName))
        {
            var genericWords = ExtractWordTokens(medicine.GenericName);
            foreach (var word in genericWords)
            {
                result.Add(new MedicineSearchToken
                {
                    MedicineId = medicine.Id,
                    Token = word,
                    TokenType = SearchTokenType.MedicineGenericWord
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Represents a parsed search token for query evaluation.
    /// </summary>
    public sealed record ParsedQueryToken(
        string RawToken,
        string NormalizedToken,
        bool IsDigitsOnly,
        string StrippedDigits,
        bool IsRecordNumberPrefix,
        string? RecordNumberDigits
    );

    /// <summary>
    /// Parses a user search query into up to maxTokens (default 6).
    /// </summary>
    public static List<ParsedQueryToken> ParseQueryTokens(string? query, int maxTokens = 6)
    {
        var list = new List<ParsedQueryToken>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return list;
        }

        var normalized = Normalize(query);
        var rawTokens = normalized.Split(QuerySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Take(maxTokens);

        foreach (var raw in rawTokens)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var isDigits = raw.All(char.IsAsciiDigit);
            var strippedDigits = isDigits ? StripLeadingZeros(raw) : string.Empty;

            var isRecordPrefix = raw.StartsWith("p-", StringComparison.OrdinalIgnoreCase) && raw.Length > 2 && raw[2..].All(char.IsAsciiDigit);
            string? recordDigits = null;
            if (isRecordPrefix)
            {
                recordDigits = StripLeadingZeros(raw[2..]);
            }

            list.Add(new ParsedQueryToken(
                RawToken: raw,
                NormalizedToken: raw,
                IsDigitsOnly: isDigits,
                StrippedDigits: strippedDigits,
                IsRecordNumberPrefix: isRecordPrefix,
                RecordNumberDigits: recordDigits
            ));
        }

        return list;
    }
}
