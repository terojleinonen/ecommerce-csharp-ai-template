using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Orders;

public sealed record OrderLineRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Range(1, Order.MaxQuantityPerItem)]
    public int Quantity { get; init; }
}

public sealed record ShippingAddressDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string FullName { get; init; }

    [Required, StringLength(200, MinimumLength = 3)]
    public required string Line1 { get; init; }

    [StringLength(200)]
    public string? Line2 { get; init; }

    [Required, StringLength(16, MinimumLength = 2)]
    public required string PostalCode { get; init; }

    [Required, StringLength(100, MinimumLength = 2)]
    public required string City { get; init; }

    [Required, StringLength(2, MinimumLength = 2)]
    [RegularExpression("^[A-Z]{2}$", ErrorMessage = "Use an ISO 3166-1 alpha-2 country code, e.g. FI.")]
    public required string Country { get; init; }
}

public sealed record CreateOrderRequest
{
    [Required, MinLength(1), MaxLength(Order.MaxDistinctItems)]
    public required IReadOnlyList<OrderLineRequest> Items { get; init; }

    [Required]
    public required ShippingAddressDto ShippingAddress { get; init; }
}

public sealed record UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; init; }
}

public sealed record OrderItemDto(int ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    IReadOnlyList<OrderItemDto> Items,
    ShippingAddressDto ShippingAddress,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    DateTimeOffset CreatedAt);

public static class OrderMapping
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id,
        o.OrderNumber,
        o.Status,
        [.. o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal))],
        new ShippingAddressDto
        {
            FullName = o.ShippingAddress.FullName,
            Line1 = o.ShippingAddress.Line1,
            Line2 = o.ShippingAddress.Line2,
            PostalCode = o.ShippingAddress.PostalCode,
            City = o.ShippingAddress.City,
            Country = o.ShippingAddress.Country,
        },
        o.Subtotal,
        o.ShippingCost,
        o.Total,
        o.CreatedAt);
}
