namespace USTHBStudy.Application.Classification;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using USTHBStudy.Domain.Classification;

/// <summary>Normalisation, validation and similarity helpers for user-proposed taxonomy values.</summary>
public static partial class TaxonomyText
{
    public const int MinLength = 2;
    public const int MaxLength = 120;

    /// <summary>Trim, collapse whitespace, NFC-normalise. Does not change letter case (display value).</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Tabs / newlines are separators; every other control character is dropped.
        var cleaned = new string(value
            .Select(c => c is '\t' or '\n' or '\r' ? ' ' : c)
            .Where(c => !char.IsControl(c))
            .ToArray());
        return Whitespace().Replace(cleaned.Normalize(NormalizationForm.FormC).Trim(), " ");
    }

    /// <summary>Accent-, case- and punctuation-insensitive comparison key (Arabic letters are preserved).</summary>
    public static string Key(string? value)
    {
        var decomposed = Normalize(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Returns an error message, or <c>null</c> when the text is acceptable for the category.</summary>
    public static string? Validate(ProposalCategory category, string normalized)
    {
        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            return $"The value must be between {MinLength} and {MaxLength} characters.";
        }

        if (normalized.IndexOfAny(new[] { '<', '>', '{', '}', '\\', '`' }) >= 0)
        {
            return "The value contains invalid characters.";
        }

        if (!normalized.Any(char.IsLetterOrDigit))
        {
            return "The value must contain letters or digits.";
        }

        if (category == ProposalCategory.AcademicYear && !TryParseAcademicYear(normalized, out _, out _))
        {
            return "Enter an academic year such as 2024-2025 (or just 2024).";
        }

        return null;
    }

    /// <summary>Parses "2024-2025", "2024/2025", "2024–2025" or "2024" into consecutive start/end years.</summary>
    public static bool TryParseAcademicYear(string text, out int start, out int end)
    {
        start = end = 0;
        var match = YearPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        start = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        end = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : start + 1;
        return start is >= 1990 and <= 2100 && end == start + 1;
    }

    public static string AcademicYearName(int start, int end) => $"{start}-{end}";

    /// <summary>True when two keys are close enough to be worth showing as "did you mean…".</summary>
    public static bool AreSimilar(string keyA, string keyB)
    {
        if (keyA.Length == 0 || keyB.Length == 0)
        {
            return false;
        }

        if (keyA == keyB)
        {
            return true;
        }

        if (keyA.Length >= 4 && keyB.Length >= 4 && (keyA.Contains(keyB, StringComparison.Ordinal) || keyB.Contains(keyA, StringComparison.Ordinal)))
        {
            return true;
        }

        var threshold = Math.Max(keyA.Length, keyB.Length) >= 8 ? 2 : 1;
        return Levenshtein(keyA, keyB) <= threshold;
    }

    public static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(\d{4})(?:\s*[-/–—]\s*(\d{4}))?$")]
    private static partial Regex YearPattern();
}
