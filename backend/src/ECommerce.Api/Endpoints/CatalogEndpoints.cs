using ECommerce.Core.Catalog;
using ECommerce.Core.Common;

namespace ECommerce.Api.Endpoints;

internal static class CatalogEndpoints
{
    public const string CacheTag = "catalog";
    public const string CachePolicy = "catalog";

    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catalog");

        group.MapGet("/products", async ([AsParameters] ProductSearchParameters p, ICatalogService catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.SearchProductsAsync(p.ToQuery(includeInactive: false), ct)))
            .WithName("SearchProducts")
            .WithSummary("Search and filter the catalog")
            .CacheOutput(CachePolicy);

        group.MapGet("/products/{id:int}", async (int id, ICatalogService catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.GetProductAsync(id, ct: ct)))
            .WithName("GetProduct")
            .Produces(StatusCodes.Status404NotFound)
            .CacheOutput(CachePolicy);

        group.MapGet("/products/{id:int}/recommendations", async (int id, int? count, ICatalogService catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.GetRecommendationsAsync(id, count ?? 4, ct)))
            .WithName("GetRecommendations")
            .WithSummary("Frequently-bought-together and similar products")
            .CacheOutput(CachePolicy);

        group.MapGet("/categories", async (ICatalogService catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.GetCategoriesAsync(ct)))
            .WithName("GetCategories")
            .CacheOutput(CachePolicy);

        return group;
    }
}

/// <summary>Query-string parameters for product search.</summary>
internal sealed record ProductSearchParameters
{
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? Search { get; init; }

    [System.ComponentModel.DataAnnotations.StringLength(80)]
    public string? Category { get; init; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? MinPrice { get; init; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? MaxPrice { get; init; }

    public bool? InStock { get; init; }

    /// <summary>relevance | newest | priceAsc | priceDesc | name (case-insensitive).</summary>
    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string? Sort { get; init; }

    [System.ComponentModel.DataAnnotations.Range(1, 10_000)]
    public int? Page { get; init; }

    [System.ComponentModel.DataAnnotations.Range(1, ProductQuery.MaxPageSize)]
    public int? PageSize { get; init; }

    public ProductQuery ToQuery(bool includeInactive) => new()
    {
        Search = Search,
        Category = Category,
        MinPrice = MinPrice,
        MaxPrice = MaxPrice,
        InStockOnly = InStock ?? false,
        IncludeInactive = includeInactive,
        Sort = Enum.TryParse<ProductSort>(Sort, ignoreCase: true, out var sort) ? sort : ProductSort.Relevance,
        Page = Page ?? 1,
        PageSize = PageSize ?? 12,
    };
}
