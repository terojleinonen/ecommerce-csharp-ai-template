using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Services;

internal sealed class CatalogService(AppDbContext db, TimeProvider clock) : ICatalogService
{
    public async Task<PagedResult<ProductDto>> SearchProductsAsync(ProductQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, ProductQuery.MaxPageSize);

        IQueryable<Product> products = db.Products.AsNoTracking().Include(p => p.Category);

        if (!query.IncludeInactive)
            products = products.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Category))
            products = products.Where(p => p.Category!.Slug == query.Category);
        if (query.MinPrice is { } min)
            products = products.Where(p => p.Price >= min);
        if (query.MaxPrice is { } max)
            products = products.Where(p => p.Price <= max);
        if (query.InStockOnly)
            products = products.Where(p => p.StockQuantity > 0);

        var terms = SearchExpressions.Tokenize(query.Search);
        if (terms.Count > 0)
            products = products.Where(SearchExpressions.MatchesAny(terms));

        var total = await products.CountAsync(ct);

        IOrderedQueryable<Product> ordered = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Price),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Price),
            ProductSort.Name => products.OrderBy(p => p.Name),
            ProductSort.Newest => products.OrderByDescending(p => p.CreatedAt),
            _ when terms.Count > 0 => products.OrderByDescending(SearchExpressions.Score(terms)),
            _ => products.OrderByDescending(p => p.StockQuantity > 0).ThenByDescending(p => p.CreatedAt),
        };

        var items = await ordered
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>([.. items.Select(p => p.ToDto())], page, pageSize, total);
    }

    public async Task<ProductDto> GetProductAsync(int id, bool includeInactive = false, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && (includeInactive || p.IsActive), ct)
            ?? throw new NotFoundException("Product", id);
        return product.ToDto();
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];

        var products = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && idList.Contains(p.Id))
            .ToListAsync(ct);

        // Preserve the caller's ordering.
        var byId = products.ToDictionary(p => p.Id);
        return [.. idList.Where(byId.ContainsKey).Select(id => byId[id].ToDto())];
    }

    /// <summary>
    /// Hybrid recommender: "frequently bought together" signals from order history,
    /// blended with same-category affinity and price proximity.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> GetRecommendationsAsync(int productId, int count = 4, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 12);
        var source = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct)
            ?? throw new NotFoundException("Product", productId);

        var coPurchases = await db.OrderItems.AsNoTracking()
            .Where(i => i.ProductId != productId
                        && db.OrderItems.Any(other => other.OrderId == i.OrderId && other.ProductId == productId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, ct);

        var coIds = coPurchases.Keys.ToList();
        var candidates = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity > 0 && p.Id != productId
                        && (p.CategoryId == source.CategoryId || coIds.Contains(p.Id)))
            .OrderBy(p => p.Id)
            .Take(100)
            .ToListAsync(ct);

        var maxCo = coPurchases.Count == 0 ? 1 : coPurchases.Values.Max();
        var ranked = candidates
            .Select(p => new
            {
                Product = p,
                Score = (coPurchases.TryGetValue(p.Id, out var c) ? 3.0 * c / maxCo : 0)
                        + (p.CategoryId == source.CategoryId ? 2.0 : 0)
                        + PriceProximity(source.Price, p.Price),
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Id)
            .Select(x => x.Product)
            .Take(count)
            .ToList();

        if (ranked.Count < count)
        {
            var exclude = ranked.Select(p => p.Id).Append(productId).ToList();
            var filler = await db.Products.AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.StockQuantity > 0 && !exclude.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Take(count - ranked.Count)
                .ToListAsync(ct);
            ranked.AddRange(filler);
        }

        return [.. ranked.Select(p => p.ToDto())];
    }

    internal static double PriceProximity(decimal a, decimal b)
    {
        var max = Math.Max(a, b);
        return max == 0 ? 1 : 1 - (double)(Math.Abs(a - b) / max);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.Products.Count(p => p.IsActive)))
            .ToListAsync(ct);

    public async Task<ProductDto> CreateProductAsync(UpsertProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        if (await db.Products.AnyAsync(p => p.Sku == request.Sku, ct))
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.", "duplicate_sku");

        var now = clock.GetUtcNow();
        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name.Trim(),
            Slug = await UniqueSlugAsync(request.Name, request.Sku, null, ct),
            CreatedAt = now,
        };
        Apply(product, request, now);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(product.Id, includeInactive: true, ct);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, UpsertProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        if (request.Sku != product.Sku && await db.Products.AnyAsync(p => p.Sku == request.Sku && p.Id != id, ct))
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.", "duplicate_sku");

        if (!string.Equals(product.Name, request.Name.Trim(), StringComparison.Ordinal))
            product.Slug = await UniqueSlugAsync(request.Name, request.Sku, id, ct);

        product.Sku = request.Sku;
        product.Name = request.Name.Trim();
        if (product.StockQuantity != request.StockQuantity)
            product.ConcurrencyStamp = Guid.NewGuid();
        Apply(product, request, clock.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The product was modified by someone else. Reload and try again.", "concurrency_conflict");
        }
        return await GetProductAsync(id, includeInactive: true, ct);
    }

    /// <summary>Archives the product. Products are never hard-deleted so order history stays intact.</summary>
    public async Task DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);
        product.IsActive = false;
        product.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Product product, UpsertProductRequest request, DateTimeOffset now)
    {
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.Price = request.Price;
        product.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl;
        product.CategoryId = request.CategoryId;
        product.StockQuantity = request.StockQuantity;
        product.IsActive = request.IsActive;
        product.UpdatedAt = now;
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
            throw new DomainException($"Category {categoryId} does not exist.", "unknown_category");
    }

    private async Task<string> UniqueSlugAsync(string name, string sku, int? excludeId, CancellationToken ct)
    {
        var slug = Slug.From(name);
        var taken = await db.Products.AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct);
        return taken ? $"{slug}-{Slug.From(sku)}" : slug;
    }
}
