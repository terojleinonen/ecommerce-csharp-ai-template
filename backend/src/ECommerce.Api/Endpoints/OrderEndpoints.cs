using ECommerce.Api.Auth;
using ECommerce.Core.Orders;

namespace ECommerce.Api.Endpoints;

internal static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        group.MapPost("/", async (CreateOrderRequest request, HttpContext http, IOrderService orders, CancellationToken ct) =>
            {
                var order = await orders.PlaceOrderAsync(http.User.GetUserId(), request, ct);
                return TypedResults.Created($"/api/orders/{order.Id}", order);
            })
            .WithName("PlaceOrder")
            .WithSummary("Checkout: prices and stock are validated server-side")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (HttpContext http, IOrderService orders, CancellationToken ct) =>
                TypedResults.Ok(await orders.GetOrdersForUserAsync(http.User.GetUserId(), ct)))
            .WithName("GetMyOrders");

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IOrderService orders, CancellationToken ct) =>
                TypedResults.Ok(await orders.GetOrderForUserAsync(http.User.GetUserId(), id, ct)))
            .WithName("GetMyOrder")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
