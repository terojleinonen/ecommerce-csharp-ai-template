using ECommerce.Core.Catalog;
using ECommerce.Core.Common;

namespace ECommerce.Core.Orders;

public enum OrderStatus
{
    Placed,
    Shipped,
    Delivered,
    Cancelled,
}

public sealed class ShippingAddress
{
    public required string FullName { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public required string PostalCode { get; set; }
    public required string City { get; set; }
    public required string Country { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class Order
{
    public const int MaxDistinctItems = 50;
    public const int MaxQuantityPerItem = 20;

    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public required ShippingAddress ShippingAddress { get; set; }
    public List<OrderItem> Items { get; } = [];
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Builds an order from catalog products. Prices are always taken from the catalog —
    /// never from the client — and stock is reserved on each product.
    /// </summary>
    public static Order Place(
        Guid userId,
        ShippingAddress address,
        IReadOnlyList<(Product Product, int Quantity)> lines,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            throw new DomainException("An order must contain at least one item.", "empty_order");
        if (lines.Count > MaxDistinctItems)
            throw new DomainException($"An order can contain at most {MaxDistinctItems} different products.", "too_many_items");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(now),
            UserId = userId,
            ShippingAddress = address,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var (product, quantity) in lines)
        {
            if (quantity > MaxQuantityPerItem)
                throw new DomainException($"You can order at most {MaxQuantityPerItem} units of '{product.Name}'.", "quantity_limit");

            product.ReserveStock(quantity);
            product.UpdatedAt = now;
            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                UnitPrice = product.Price,
                Quantity = quantity,
            });
        }

        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.ShippingCost = OrderPricing.ShippingFor(order.Subtotal);
        order.Total = order.Subtotal + order.ShippingCost;
        return order;
    }

    public void ChangeStatus(OrderStatus next, DateTimeOffset now)
    {
        var allowed = (Status, next) switch
        {
            (OrderStatus.Placed, OrderStatus.Shipped) => true,
            (OrderStatus.Placed, OrderStatus.Cancelled) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false,
        };
        if (!allowed)
            throw new DomainException($"Cannot change order status from {Status} to {next}.", "invalid_status_transition");

        Status = next;
        UpdatedAt = now;
    }

    private static string GenerateOrderNumber(DateTimeOffset now) =>
        $"ORD-{now:yyyyMMdd}-{Convert.ToHexString(Guid.NewGuid().ToByteArray(), 0, 3)}";
}

public static class OrderPricing
{
    public const decimal FreeShippingThreshold = 50m;
    public const decimal StandardShipping = 4.90m;

    public static decimal ShippingFor(decimal subtotal) =>
        subtotal >= FreeShippingThreshold ? 0m : StandardShipping;
}
