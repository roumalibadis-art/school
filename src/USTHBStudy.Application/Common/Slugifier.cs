namespace USTHBStudy.Application.Common;

using System.Globalization;
using System.Text;

/// <summary>
/// Produces URL-safe slugs (PRD §54): accents folded, lower-cased, non-alphanumerics collapsed to
/// single hyphens, trimmed. Uniqueness is the caller's responsibility (append <c>-2</c>, <c>-3</c>…).
/// </summary>
public static class Slugifier
{
    public static string Slugify(string value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasHyphen = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                builder.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].Trim('-');
        }

        return slug;
    }
}
