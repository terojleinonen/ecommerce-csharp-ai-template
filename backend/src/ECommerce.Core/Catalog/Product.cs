using ECommerce.Core.Common;

namespace ECommerce.Core.Catalog;

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token, regenerated on every stock change.</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public bool IsAvailable => IsActive && StockQuantity > 0;

    /// <summary>Decrements stock for an order line, enforcing availability rules.</summary>
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive.", "invalid_quantity");
        if (!IsActive)
            throw new DomainException($"'{Name}' is no longer available.", "product_unavailable");
        if (StockQuantity < quantity)
            throw new DomainException(
                $"Only {StockQuantity} unit(s) of '{Name}' left in stock.", "insufficient_stock");

        StockQuantity -= quantity;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
