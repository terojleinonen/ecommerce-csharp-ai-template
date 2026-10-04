using System.Globalization;
using System.Text.RegularExpressions;

namespace ECommerce.Infrastructure.AI;

/// <summary>
/// The assistant tags recommended products inline as <c>[[product:42]]</c>. We extract those IDs
/// (to return real product cards) and strip the markers from the visible reply.
/// </summary>
internal static partial class ProductReferences
{
    public const string Instruction =
        "When you recommend or mention a specific product, write its marker [[product:ID]] immediately after its name, " +
        "using the id returned by the tools (for example: Trail Runner GTX [[product:5]]). Only use ids you received from a tool.";

    [GeneratedRegex(@"\s?\[\[product:(\d{1,9})\]\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex MarkerRegex();

    public static (string CleanText, IReadOnlyList<int> ProductIds) Extract(string text, int max = 6)
    {
        var ids = new List<int>();
        foreach (Match m in MarkerRegex().Matches(text))
        {
            var id = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!ids.Contains(id) && ids.Count < max) ids.Add(id);
        }
        var clean = MarkerRegex().Replace(text, string.Empty).Trim();
        return (clean, ids);
    }
}
