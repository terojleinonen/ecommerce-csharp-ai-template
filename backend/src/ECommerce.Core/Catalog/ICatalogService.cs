using ECommerce.Core.Common;

namespace ECommerce.Core.Catalog;

public interface ICatalogService
{
    Task<PagedResult<ProductDto>> SearchProductsAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDto> GetProductAsync(int id, bool includeInactive = false, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetRecommendationsAsync(int productId, int count = 4, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    Task<ProductDto> CreateProductAsync(UpsertProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateProductAsync(int id, UpsertProductRequest request, CancellationToken ct = default);
    Task DeleteProductAsync(int id, CancellationToken ct = default);
}
