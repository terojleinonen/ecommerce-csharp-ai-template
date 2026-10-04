using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Catalog;

public sealed record CategoryDto(int Id, string Name, string Slug, string? Description, int ProductCount);

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    string? ImageUrl,
    int CategoryId,
    string Category,
    int StockQuantity,
    bool IsActive);

public enum ProductSort
{
    Relevance,
    Newest,
    PriceAsc,
    PriceDesc,
    Name,
}

public sealed record ProductQuery
{
    public string? Search { get; init; }
    public string? Category { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool InStockOnly { get; init; }
    public bool IncludeInactive { get; init; }
    public ProductSort Sort { get; init; } = ProductSort.Relevance;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;

    public const int MaxPageSize = 100;
}

public sealed record UpsertProductRequest
{
    [Required, StringLength(32, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "SKU may only contain upper-case letters, digits and dashes.")]
    public required string Sku { get; init; }

    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "A category is required.")]
    public int CategoryId { get; init; }

    [Range(0, 1_000_000)]
    public int StockQuantity { get; init; }

    public bool IsActive { get; init; } = true;
}

public static class ProductMapping
{
    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Sku, p.Name, p.Slug, p.Description, p.Price, p.ImageUrl,
        p.CategoryId, p.Category?.Name ?? string.Empty, p.StockQuantity, p.IsActive);
}
