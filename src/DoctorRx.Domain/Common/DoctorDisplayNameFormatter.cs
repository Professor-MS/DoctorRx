using System;

namespace DoctorRx.Domain.Common;

/// <summary>
/// Formats doctor display names with clean title prefix handling and deduplication.
/// Prevents awkward duplicate titles like 'Dr. Dr. Tariq' while respecting academic/honorific titles and Urdu prefixes.
/// </summary>
public static class DoctorDisplayNameFormatter
{
    public static string Format(string? titlePrefix, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var cleanName = name.Trim();
        var cleanPrefix = titlePrefix?.Trim();

        if (string.IsNullOrWhiteSpace(cleanPrefix))
            return cleanName;

        // If the name exactly starts with the prefix (e.g. "Dr." + "Dr. Tariq" or "ڈاکٹر" + "ڈاکٹر طارق")
        if (cleanName.StartsWith(cleanPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = cleanName.Substring(cleanPrefix.Length).TrimStart(' ', '.', ',');
            return $"{cleanPrefix} {remainder}".Trim();
        }

        // Punctuation tolerance: "Dr" prefix with "Dr. Tariq" name, or "Dr." prefix with "Dr Tariq" name
        var prefixWithoutPunctuation = cleanPrefix.TrimEnd('.', ' ');
        if (cleanName.StartsWith(prefixWithoutPunctuation + ".", StringComparison.OrdinalIgnoreCase) ||
            cleanName.StartsWith(prefixWithoutPunctuation + " ", StringComparison.OrdinalIgnoreCase) ||
            cleanName.Equals(prefixWithoutPunctuation, StringComparison.OrdinalIgnoreCase))
        {
            var dotIndex = cleanName.IndexOfAny(new[] { '.', ' ' });
            var remainder = dotIndex >= 0 ? cleanName.Substring(dotIndex + 1).TrimStart(' ', '.', ',') : string.Empty;
            return $"{cleanPrefix} {remainder}".Trim();
        }

        // Hierarchical prefix: prefix is "Prof. Dr." and name starts with "Dr." or "Dr "
        if ((cleanPrefix.Equals("Prof. Dr.", StringComparison.OrdinalIgnoreCase) ||
             cleanPrefix.Equals("Prof. Dr", StringComparison.OrdinalIgnoreCase)) &&
            (cleanName.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase)))
        {
            var dotIndex = cleanName.IndexOfAny(new[] { '.', ' ' });
            var remainder = dotIndex >= 0 ? cleanName.Substring(dotIndex + 1).TrimStart(' ', '.', ',') : string.Empty;
            return $"{cleanPrefix} {remainder}".Trim();
        }

        // If prefix is simple "Dr." or "Dr" and name already has a higher honorific title like "Prof." or "Prof. Dr."
        if ((cleanPrefix.Equals("Dr.", StringComparison.OrdinalIgnoreCase) ||
             cleanPrefix.Equals("Dr", StringComparison.OrdinalIgnoreCase)) &&
            (cleanName.StartsWith("Prof.", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("Prof ", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("Assoc. Prof.", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("Asst. Prof.", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("ڈاکٹر ", StringComparison.OrdinalIgnoreCase) ||
             cleanName.StartsWith("حکیم ", StringComparison.OrdinalIgnoreCase)))
        {
            return cleanName;
        }

        // Urdu prefix deduplication
        if (cleanPrefix == "ڈاکٹر" && cleanName.StartsWith("ڈاکٹر", StringComparison.Ordinal))
        {
            var remainder = cleanName.Substring("ڈاکٹر".Length).Trim();
            return $"ڈاکٹر {remainder}".Trim();
        }

        if (cleanPrefix == "حکیم" && cleanName.StartsWith("حکیم", StringComparison.Ordinal))
        {
            var remainder = cleanName.Substring("حکیم".Length).Trim();
            return $"حکیم {remainder}".Trim();
        }

        return $"{cleanPrefix} {cleanName}";
    }
}
