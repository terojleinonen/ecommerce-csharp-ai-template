using ECommerce.Core.Common;
using ECommerce.Core.Orders;
using ECommerce.Infrastructure.Services;
using ECommerce.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.UnitTests.Services;

public sealed class OrderServiceTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync();
    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    private OrderService CreateService() => new(_db.CreateContext(), TestDatabase.Clock);

    private static CreateOrderRequest Request(params (int ProductId, int Quantity)[] lines) => new()
    {
        Items = [.. lines.Select(l => new OrderLineRequest { ProductId = l.ProductId, Quantity = l.Quantity })],
        ShippingAddress = new ShippingAddressDto
        {
            FullName = " Ada ", Line1 = "Main 1", PostalCode = "00100", City = "Helsinki", Country = "fi",
        },
    };

    [Fact]
    public async Task PlaceOrder_merges_duplicate_lines_and_decrements_stock()
    {
        var order = await CreateService().PlaceOrderAsync(TestDatabase.UserId, Request((4, 1), (4, 2), (5, 1)));

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(3, order.Items.Single(i => i.ProductId == 4).Quantity);
        Assert.Equal(49.90m * 3 + 89m, order.Subtotal);
        Assert.Equal(0m, order.ShippingCost);
        Assert.Equal("FI", order.ShippingAddress.Country);
        Assert.Equal("Ada", order.ShippingAddress.FullName);

        await using var ctx = _db.CreateContext();
        Assert.Equal(17, (await ctx.Products.SingleAsync(p => p.Id == 4)).StockQuantity);
    }

    [Fact]
    public async Task PlaceOrder_rejects_unknown_products_and_leaves_stock_untouched()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateService().PlaceOrderAsync(TestDatabase.UserId, Request((4, 1), (999, 1))));
        Assert.Equal("unknown_product", ex.Code);

        await using var ctx = _db.CreateContext();
        Assert.Equal(20, (await ctx.Products.SingleAsync(p => p.Id == 4)).StockQuantity);
    }

    [Fact]
    public async Task Orders_are_scoped_to_their_owner()
    {
        var order = await CreateService().PlaceOrderAsync(TestDatabase.UserId, Request((4, 1)));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetOrderForUserAsync(Guid.NewGuid(), order.Id));
        Assert.Single(await CreateService().GetOrdersForUserAsync(TestDatabase.UserId));
        Assert.Empty(await CreateService().GetOrdersForUserAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Cancelling_an_order_restocks_products()
    {
        var order = await CreateService().PlaceOrderAsync(TestDatabase.UserId, Request((5, 3)));

        var cancelled = await CreateService().UpdateStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        await using var ctx = _db.CreateContext();
        Assert.Equal(8, (await ctx.Products.SingleAsync(p => p.Id == 5)).StockQuantity);
    }
}
