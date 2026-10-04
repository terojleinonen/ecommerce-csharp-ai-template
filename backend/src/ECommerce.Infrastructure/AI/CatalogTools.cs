using System.Globalization;
using System.Text.Json;
using ECommerce.Core.Catalog;
using ECommerce.Core.Common;

namespace ECommerce.Infrastructure.AI;

/// <summary>
/// Read-only catalog tools exposed to the LLM. Every result comes from the database, which keeps the
/// assistant grounded: it cannot recommend products, prices or stock levels that don't exist.
/// </summary>
internal sealed class CatalogTools(ICatalogService catalog)
{
    public const string SearchProducts = "search_products";
    public const string GetProductDetails = "get_product_details";
    public const string ListCategories = "list_categories";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record ToolDefinition(string Name, string Description, IReadOnlyDictionary<string, JsonElement> Properties, IReadOnlyList<string> Required);

    public static IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        new(SearchProducts,
            "Search the store's product catalog. Returns up to 6 active products ranked by relevance, " +
            "with id, name, category, price (EUR), stock and a short summary. Use it whenever the shopper asks for " +
            "products, gift ideas, comparisons or anything that depends on what the store sells.",
            new Dictionary<string, JsonElement>
            {
                ["query"] = Schema(new { type = "string", description = "Keywords describing what the shopper wants, e.g. 'waterproof running shoes'." }),
                ["category"] = Schema(new { type = "string", description = "Optional category slug from list_categories, e.g. 'footwear'." }),
                ["max_price"] = Schema(new { type = "number", description = "Optional maximum price in EUR." }),
                ["min_price"] = Schema(new { type = "number", description = "Optional minimum price in EUR." }),
                ["in_stock_only"] = Schema(new { type = "boolean", description = "Only return products that are in stock. Defaults to true." }),
            },
            ["query"]),
        new(GetProductDetails,
            "Get full details for one product by id, including its complete description, stock level and products " +
            "that are frequently bought together with it.",
            new Dictionary<string, JsonElement>
            {
                ["product_id"] = Schema(new { type = "integer", description = "Product id from search_products." }),
            },
            ["product_id"]),
        new(ListCategories,
            "List all product categories with their slugs and number of products.",
            new Dictionary<string, JsonElement>(),
            []),
    ];

    /// <summary>Product ids the tools have shown to the model during this conversation turn.</summary>
    public HashSet<int> SeenProductIds { get; } = [];

    public async Task<(string Content, bool IsError)> ExecuteAsync(
        string name, IReadOnlyDictionary<string, JsonElement> input, CancellationToken ct)
    {
        try
        {
            return name switch
            {
                SearchProducts => (await SearchAsync(input, ct), false),
                GetProductDetails => (await DetailsAsync(input, ct), false),
                ListCategories => (JsonSerializer.Serialize(await catalog.GetCategoriesAsync(ct), Json), false),
                _ => ($"Unknown tool '{name}'.", true),
            };
        }
        catch (NotFoundException ex)
        {
            return (ex.Message, true);
        }
        catch (ArgumentException ex)
        {
            return ($"Invalid input: {ex.Message}", true);
        }
    }

    private async Task<string> SearchAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken ct)
    {
        var query = new ProductQuery
        {
            Search = GetString(input, "query") ?? throw new ArgumentException("'query' is required."),
            Category = GetString(input, "category"),
            MaxPrice = GetDecimal(input, "max_price"),
            MinPrice = GetDecimal(input, "min_price"),
            InStockOnly = GetBool(input, "in_stock_only") ?? true,
            PageSize = 6,
        };
        var result = await catalog.SearchProductsAsync(query, ct);
        foreach (var p in result.Items) SeenProductIds.Add(p.Id);

        return JsonSerializer.Serialize(new
        {
            total_matches = result.TotalCount,
            products = result.Items.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                category = p.Category,
                price_eur = p.Price,
                in_stock = p.StockQuantity,
                summary = Truncate(p.Description, 180),
            }),
        }, Json);
    }

    private async Task<string> DetailsAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken ct)
    {
        var id = GetInt(input, "product_id") ?? throw new ArgumentException("'product_id' must be an integer.");
        var product = await catalog.GetProductAsync(id, ct: ct);
        var related = await catalog.GetRecommendationsAsync(id, 3, ct);
        SeenProductIds.Add(product.Id);
        foreach (var r in related) SeenProductIds.Add(r.Id);

        return JsonSerializer.Serialize(new
        {
            id = product.Id,
            name = product.Name,
            sku = product.Sku,
            category = product.Category,
            price_eur = product.Price,
            in_stock = product.StockQuantity,
            description = product.Description,
            frequently_bought_with = related.Select(r => new { id = r.Id, name = r.Name, price_eur = r.Price }),
        }, Json);
    }

    private static JsonElement Schema(object value) => JsonSerializer.SerializeToElement(value);

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static decimal? GetDecimal(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private static int? GetInt(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return i;
        return null;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
