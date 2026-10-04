using System.Globalization;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;

namespace ECommerce.Infrastructure.AI;

/// <summary>Template-based copy so the admin tooling works without an LLM key.</summary>
internal sealed class OfflineProductCopywriter : IProductCopywriter
{
    public string Provider => "offline";

    public Task<string> WriteDescriptionAsync(ProductDto product, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        var basis = string.IsNullOrWhiteSpace(product.Description)
            ? $"The {product.Name} is a dependable everyday pick from our {product.Category.ToLowerInvariant()} range."
            : product.Description.Trim();

        var text = string.Create(CultureInfo.InvariantCulture, $"""
            {basis}

            • Selected by the ShopSense team for quality and value
            • Free shipping when your order reaches €50
            • 30-day hassle-free returns
            """);
        return Task.FromResult(text);
    }
}
