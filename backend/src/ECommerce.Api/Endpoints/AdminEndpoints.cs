using System.ComponentModel.DataAnnotations;
using ECommerce.Api.Auth;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using ECommerce.Core.Orders;
using Microsoft.AspNetCore.OutputCaching;

namespace ECommerce.Api.Endpoints;

internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(AuthConstants.AdminPolicy);

        group.MapGet("/products", async ([AsParameters] ProductSearchParameters p, ICatalogService catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.SearchProductsAsync(p.ToQuery(includeInactive: true), ct)))
            .WithName("AdminListProducts");

        group.MapPost("/products", async (UpsertProductRequest request, ICatalogService catalog, IOutputCacheStore cache, CancellationToken ct) =>
            {
                var product = await catalog.CreateProductAsync(request, ct);
                await cache.EvictByTagAsync(CatalogEndpoints.CacheTag, ct);
                return TypedResults.Created($"/api/products/{product.Id}", product);
            })
            .WithName("CreateProduct")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/products/{id:int}", async (int id, UpsertProductRequest request, ICatalogService catalog, IOutputCacheStore cache, CancellationToken ct) =>
            {
                var product = await catalog.UpdateProductAsync(id, request, ct);
                await cache.EvictByTagAsync(CatalogEndpoints.CacheTag, ct);
                return TypedResults.Ok(product);
            })
            .WithName("UpdateProduct")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/products/{id:int}", async (int id, ICatalogService catalog, IOutputCacheStore cache, CancellationToken ct) =>
            {
                await catalog.DeleteProductAsync(id, ct);
                await cache.EvictByTagAsync(CatalogEndpoints.CacheTag, ct);
                return TypedResults.NoContent();
            })
            .WithName("ArchiveProduct")
            .WithSummary("Archives (soft-deletes) a product");

        group.MapPost("/ai/product-description", async (ProductDescriptionDraft draft, ICatalogService catalog, IProductCopywriter copywriter, CancellationToken ct) =>
            {
                var categories = await catalog.GetCategoriesAsync(ct);
                var category = categories.FirstOrDefault(c => c.Id == draft.CategoryId)?.Name ?? "General";
                var product = new ProductDto(0, "DRAFT", draft.Name, "", draft.Description, draft.Price, null,
                    draft.CategoryId, category, 0, true);
                var description = await copywriter.WriteDescriptionAsync(product, ct);
                return TypedResults.Ok(new GeneratedDescriptionDto(description, copywriter.Provider));
            })
            .WithName("GenerateProductDescription")
            .WithSummary("Drafts product copy with AI (not saved)")
            .RequireRateLimiting(AssistantEndpoints.RateLimitPolicy);

        group.MapGet("/orders", async (int? page, int? pageSize, IOrderService orders, CancellationToken ct) =>
                TypedResults.Ok(await orders.ListOrdersAsync(page ?? 1, pageSize ?? 20, ct)))
            .WithName("AdminListOrders");

        group.MapPatch("/orders/{id:guid}/status", async (Guid id, UpdateOrderStatusRequest request, IOrderService orders, IOutputCacheStore cache, CancellationToken ct) =>
            {
                var order = await orders.UpdateStatusAsync(id, request.Status, ct);
                if (request.Status == OrderStatus.Cancelled)
                    await cache.EvictByTagAsync(CatalogEndpoints.CacheTag, ct); // stock changed
                return TypedResults.Ok(order);
            })
            .WithName("UpdateOrderStatus")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}

internal sealed record ProductDescriptionDraft
{
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Price { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }
}
