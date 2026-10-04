using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using ECommerce.Infrastructure.Services;

namespace ECommerce.Infrastructure.AI;

/// <summary>
/// Deterministic, catalog-grounded assistant used when no LLM key is configured (or the LLM is down).
/// It understands a few intents (greetings, store policies, price limits) and otherwise runs a catalog search.
/// </summary>
internal sealed partial class OfflineShoppingAssistant(ICatalogService catalog) : IShoppingAssistant
{
    public string Provider => "offline";

    [GeneratedRegex(@"(?:under|below|less than|cheaper than|max(?:imum)?|up to|<)\s*€?\s*(\d+(?:[.,]\d+)?)\s*(?:€|eur|euros?)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex MaxPriceRegex();

    [GeneratedRegex(@"\b(hi|hello|hey|moi|hei|good (morning|afternoon|evening))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex GreetingRegex();

    public async Task<AssistantChatResponse> ReplyAsync(IReadOnlyList<ChatMessageDto> conversation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var message = conversation.LastOrDefault(m => m.Role == ChatRole.User)?.Content.Trim() ?? string.Empty;
        var lower = message.ToLowerInvariant();

        if (ContainsAny(lower, "shipping", "delivery", "deliver"))
            return Reply("Shipping is free on orders of €50 or more; otherwise it's €4.90. Delivery takes 2–4 business days within the EU.");
        if (ContainsAny(lower, "return", "refund", "exchange"))
            return Reply("You can return unused items within 30 days for a full refund. Just keep the original packaging.");

        var maxPrice = ParseMaxPrice(message);
        var searchText = MaxPriceRegex().Replace(message, " ");
        var terms = SearchExpressions.Tokenize(searchText);

        if (terms.Count == 0)
        {
            if (GreetingRegex().IsMatch(message) || maxPrice is null)
            {
                var categories = await catalog.GetCategoriesAsync(ct);
                return Reply("Hi! I'm the ShopSense assistant. Tell me what you're looking for, for example " +
                             "\"waterproof running shoes\" or \"a gift under 50 €\". We stock " +
                             string.Join(", ", categories.Select(c => c.Name)) + ".");
            }
        }

        var result = await catalog.SearchProductsAsync(new ProductQuery
        {
            Search = string.Join(' ', terms),
            MaxPrice = maxPrice,
            InStockOnly = true,
            PageSize = 3,
            Sort = terms.Count == 0 ? ProductSort.PriceDesc : ProductSort.Relevance,
        }, ct);

        if (result.Items.Count == 0)
        {
            return Reply("I couldn't find anything matching that in our catalog. Try different keywords, " +
                         "or browse a category from the menu.");
        }

        // Products are rendered as cards by the client, so the text only summarizes.
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Here {(result.Items.Count == 1 ? "is a pick" : $"are {result.Items.Count} picks")} from our catalog");
        if (maxPrice is { } mp) sb.Append(CultureInfo.InvariantCulture, $" under €{mp:0.##}");
        sb.Append('.');
        var lowStock = result.Items.Where(p => p.StockQuantity < 5).Select(p => p.Name).ToList();
        if (lowStock.Count > 0) sb.Append(CultureInfo.InvariantCulture, $" Heads up: only a few left of {string.Join(" and ", lowStock)}.");

        return new AssistantChatResponse(sb.ToString(), result.Items, Provider);
    }

    internal static decimal? ParseMaxPrice(string message)
    {
        var m = MaxPriceRegex().Match(message);
        return m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private AssistantChatResponse Reply(string text) => new(text, [], Provider);

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.Ordinal));
}
