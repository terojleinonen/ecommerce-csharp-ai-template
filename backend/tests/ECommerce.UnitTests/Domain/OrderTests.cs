using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using ECommerce.Core.Orders;

namespace ECommerce.UnitTests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static Product NewProduct(decimal price = 10m, int stock = 5, bool active = true) => new()
    {
        Id = 1, Sku = "SKU-1", Name = "Thing", Slug = "thing", Price = price, StockQuantity = stock, IsActive = active,
    };

    private static ShippingAddress Address() => new()
    {
        FullName = "Ada Lovelace", Line1 = "Main St 1", PostalCode = "00100", City = "Helsinki", Country = "FI",
    };

    [Fact]
    public void Place_computes_totals_from_catalog_prices_and_charges_shipping_below_threshold()
    {
        var product = NewProduct(price: 12.50m);

        var order = Order.Place(Guid.NewGuid(), Address(), [(product, 2)], Now);

        Assert.Equal(25.00m, order.Subtotal);
        Assert.Equal(OrderPricing.StandardShipping, order.ShippingCost);
        Assert.Equal(25.00m + OrderPricing.StandardShipping, order.Total);
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.StartsWith("ORD-20261001-", order.OrderNumber, StringComparison.Ordinal);
    }

    [Fact]
    public void Place_gives_free_shipping_at_threshold()
    {
        var order = Order.Place(Guid.NewGuid(), Address(), [(NewProduct(price: 25m), 2)], Now);

        Assert.Equal(0m, order.ShippingCost);
        Assert.Equal(50m, order.Total);
    }

    [Fact]
    public void Place_reserves_stock_and_snapshots_product_data()
    {
        var product = NewProduct(stock: 5);
        var stampBefore = product.ConcurrencyStamp;

        var order = Order.Place(Guid.NewGuid(), Address(), [(product, 3)], Now);

        Assert.Equal(2, product.StockQuantity);
        Assert.NotEqual(stampBefore, product.ConcurrencyStamp);
        var item = Assert.Single(order.Items);
        Assert.Equal(("Thing", "SKU-1", 10m, 3), (item.ProductName, item.Sku, item.UnitPrice, item.Quantity));
    }

    [Fact]
    public void Place_rejects_insufficient_stock()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Order.Place(Guid.NewGuid(), Address(), [(NewProduct(stock: 1), 2)], Now));
        Assert.Equal("insufficient_stock", ex.Code);
    }

    [Fact]
    public void Place_rejects_inactive_products()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Order.Place(Guid.NewGuid(), Address(), [(NewProduct(active: false), 1)], Now));
        Assert.Equal("product_unavailable", ex.Code);
    }

    [Fact]
    public void Place_rejects_empty_orders_and_quantity_above_limit()
    {
        Assert.Equal("empty_order", Assert.Throws<DomainException>(() =>
            Order.Place(Guid.NewGuid(), Address(), [], Now)).Code);
        Assert.Equal("quantity_limit", Assert.Throws<DomainException>(() =>
            Order.Place(Guid.NewGuid(), Address(), [(NewProduct(stock: 100), Order.MaxQuantityPerItem + 1)], Now)).Code);
    }

    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void ChangeStatus_allows_valid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Place(Guid.NewGuid(), Address(), [(NewProduct(), 1)], Now);
        order.Status = from;

        order.ChangeStatus(to, Now.AddDays(1));

        Assert.Equal(to, order.Status);
        Assert.Equal(Now.AddDays(1), order.UpdatedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Placed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    public void ChangeStatus_rejects_invalid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Place(Guid.NewGuid(), Address(), [(NewProduct(), 1)], Now);
        order.Status = from;

        Assert.Throws<DomainException>(() => order.ChangeStatus(to, Now));
    }
}
