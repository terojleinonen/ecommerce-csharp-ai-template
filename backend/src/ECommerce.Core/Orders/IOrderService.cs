using ECommerce.Core.Common;

namespace ECommerce.Core.Orders;

public interface IOrderService
{
    Task<OrderDto> PlaceOrderAsync(Guid userId, CreateOrderRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> GetOrdersForUserAsync(Guid userId, CancellationToken ct = default);
    Task<OrderDto> GetOrderForUserAsync(Guid userId, Guid orderId, CancellationToken ct = default);

    Task<PagedResult<OrderDto>> ListOrdersAsync(int page, int pageSize, CancellationToken ct = default);
    Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default);
}
