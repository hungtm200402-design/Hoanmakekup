using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Endpoints;

public static class AdminOrderEndpoints
{
    public static async Task<IResult> GetAsync(BeautyDbContext db, CancellationToken cancellationToken)
    {
        var rows = (await db.Orders
            .Include(order => order.Items)
            .ToListAsync(cancellationToken))
            .OrderByDescending(order => order.CreatedAt)
            .ToList();
        return Results.Ok(rows);
    }

    public static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        BeautyDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return Results.NotFound();
        }

        if (!CanTransition(order.Status, request.Status))
        {
            return Results.BadRequest(new { error = "Không thể chuyển trạng thái đơn hàng theo thao tác này." });
        }

        if (request.Status == OrderStatus.Cancelled)
        {
            var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await db.Products.Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
            foreach (var item in order.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.Stock += item.Quantity;
                }
            }
        }

        order.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(order);
    }

    private static bool CanTransition(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.Pending => to is OrderStatus.Paid or OrderStatus.Cancelled,
        OrderStatus.Paid => to is OrderStatus.Shipping or OrderStatus.Cancelled,
        OrderStatus.Shipping => to is OrderStatus.Completed,
        _ => false
    };
}
