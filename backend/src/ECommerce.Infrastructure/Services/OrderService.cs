using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using ECommerce.Core.Orders;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Services;

internal sealed class OrderService(AppDbContext db, TimeProvider clock) : IOrderService
{
    private const int MaxConcurrencyRetries = 3;

    public async Task<OrderDto> PlaceOrderAsync(Guid userId, CreateOrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Merge duplicate lines so stock is checked against the real total per product.
        var quantities = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        for (var attempt = 1; ; attempt++)
        {
            var products = await db.Products
                .Where(p => quantities.Keys.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var missing = quantities.Keys.FirstOrDefault(id => !products.ContainsKey(id));
            if (missing != 0)
                throw new DomainException($"Product {missing} does not exist.", "unknown_product");

            var lines = quantities
                .Select(kv => (products[kv.Key], kv.Value))
                .ToList();

            var order = Order.Place(userId, ToAddress(request.ShippingAddress), lines, clock.GetUtcNow());
            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync(ct);
                return order.ToDto();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Someone bought the same product concurrently: reload stock and retry.
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("Stock changed while placing your order. Please try again.", "stock_conflict");
            }
        }
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return [.. orders.Select(o => o.ToDto())];
    }

    public async Task<OrderDto> GetOrderForUserAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        // Filtering by user means other customers' orders are indistinguishable from missing ones.
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order", orderId);
        return order.ToDto();
    }

    public async Task<PagedResult<OrderDto>> ListOrdersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await db.Orders.CountAsync(ct);
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<OrderDto>([.. orders.Select(o => o.ToDto())], page, pageSize, total);
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException("Order", orderId);

        var now = clock.GetUtcNow();
        order.ChangeStatus(status, now);

        if (status == OrderStatus.Cancelled)
            await RestockAsync(order, now, ct);

        await db.SaveChangesAsync(ct);
        return order.ToDto();
    }

    private async Task RestockAsync(Order order, DateTimeOffset now, CancellationToken ct)
    {
        var ids = order.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in order.Items)
        {
            if (!products.TryGetValue(item.ProductId, out Product? product)) continue;
            product.StockQuantity += item.Quantity;
            product.ConcurrencyStamp = Guid.NewGuid();
            product.UpdatedAt = now;
        }
    }

    private static ShippingAddress ToAddress(ShippingAddressDto dto) => new()
    {
        FullName = dto.FullName.Trim(),
        Line1 = dto.Line1.Trim(),
        Line2 = string.IsNullOrWhiteSpace(dto.Line2) ? null : dto.Line2.Trim(),
        PostalCode = dto.PostalCode.Trim(),
        City = dto.City.Trim(),
        Country = dto.Country.ToUpperInvariant(),
    };
}
