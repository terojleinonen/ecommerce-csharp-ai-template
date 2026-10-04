using System.Globalization;
using System.Text;

namespace ECommerce.Core.Common;

public static class Slug
{
    /// <summary>Creates a URL-friendly slug: "Trail Runner 2.0!" → "trail-runner-2-0".</summary>
    public static string From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var lastWasDash = true;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }
        return sb.ToString().TrimEnd('-');
    }
}
