using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using ECommerce.Core.Orders;
using ECommerce.Infrastructure.Services;
using ECommerce.UnitTests.TestSupport;

namespace ECommerce.UnitTests.Services;

public sealed class CatalogServiceTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();
    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    private CatalogService CreateService() => new(_db.CreateContext(), TestDatabase.Clock);

    [Fact]
    public async Task Search_ranks_name_matches_first_and_hides_inactive_products()
    {
        var result = await CreateService().SearchProductsAsync(new ProductQuery { Search = "waterproof shoes" });

        Assert.Equal("Trail Runner GTX", result.Items[0].Name); // name+description hit
        Assert.DoesNotContain(result.Items, p => p.Name == "Archived Mug");
        Assert.Contains(result.Items, p => p.Name == "Winter Boot"); // description hit
    }

    [Fact]
    public async Task Search_applies_category_price_and_stock_filters()
    {
        var result = await CreateService().SearchProductsAsync(new ProductQuery
        {
            Category = "footwear", MaxPrice = 160m, InStockOnly = true, Sort = ProductSort.PriceAsc,
        });

        Assert.Equal(["Canvas Sneaker", "Trail Runner GTX"], result.Items.Select(p => p.Name));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_paginates_and_clamps_page_size()
    {
        var page2 = await CreateService().SearchProductsAsync(new ProductQuery { Page = 2, PageSize = 2, Sort = ProductSort.Name });
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);

        var huge = await CreateService().SearchProductsAsync(new ProductQuery { PageSize = 10_000 });
        Assert.Equal(ProductQuery.MaxPageSize, huge.PageSize);
    }

    [Fact]
    public async Task Recommendations_prefer_frequently_bought_together_products()
    {
        // Coffee set (4) is bought together with the trail runner (1) in two orders.
        await using (var ctx = _db.CreateContext())
        {
            foreach (var _ in Enumerable.Range(0, 2))
            {
                var products = ctx.Products.Where(p => p.Id == 1 || p.Id == 4).ToList();
                ctx.Orders.Add(Order.Place(TestDatabase.UserId, Address(), [.. products.Select(p => (p, 1))], TestDatabase.Now));
            }
            await ctx.SaveChangesAsync();
        }

        var recs = await CreateService().GetRecommendationsAsync(1, 3);

        Assert.Equal("Pour-Over Coffee Set", recs[0].Name);
        Assert.Contains(recs, p => p.Name == "Canvas Sneaker"); // same category
        Assert.DoesNotContain(recs, p => p.Id == 1 || p.Name == "Winter Boot"); // self / out of stock
    }

    [Fact]
    public async Task Create_rejects_duplicate_sku_and_unknown_category()
    {
        var service = CreateService();
        var request = new UpsertProductRequest { Sku = "SKU-001", Name = "Dup", Price = 1m, CategoryId = 1 };

        var dup = await Assert.ThrowsAsync<ConflictException>(() => service.CreateProductAsync(request));
        Assert.Equal("duplicate_sku", dup.Code);

        var badCat = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateProductAsync(request with { Sku = "NEW-1", CategoryId = 999 }));
        Assert.Equal("unknown_category", badCat.Code);
    }

    [Fact]
    public async Task Create_generates_unique_slug_and_delete_archives()
    {
        var service = CreateService();
        var created = await service.CreateProductAsync(new UpsertProductRequest
        {
            Sku = "NEW-TRAIL", Name = "Trail Runner GTX", Price = 99m, CategoryId = 1, StockQuantity = 3,
        });
        Assert.Equal("trail-runner-gtx-new-trail", created.Slug);

        await CreateService().DeleteProductAsync(created.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetProductAsync(created.Id));
        var archived = await CreateService().GetProductAsync(created.Id, includeInactive: true);
        Assert.False(archived.IsActive);
    }

    [Theory]
    [InlineData(100, 100, 1.0)]
    [InlineData(100, 50, 0.5)]
    [InlineData(0, 0, 1.0)]
    public void PriceProximity_is_relative(double a, double b, double expected) =>
        Assert.Equal(expected, CatalogService.PriceProximity((decimal)a, (decimal)b), 3);

    internal static ShippingAddress Address() => new()
    {
        FullName = "Test User", Line1 = "Street 1", PostalCode = "00100", City = "Helsinki", Country = "FI",
    };
}
