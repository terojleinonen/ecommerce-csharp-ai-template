namespace ECommerce.Core.DTOs;

public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    string? Category
);
