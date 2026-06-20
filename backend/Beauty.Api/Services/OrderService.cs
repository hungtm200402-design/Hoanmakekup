using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Services;

public sealed class OrderService(BeautyDbContext db)
{
    public async Task<(bool Created, string Message, Order? Order)> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return (false, "Đơn hàng phải có sản phẩm.", null);
        }

        var productIds = request.Items.Select(item => item.ProductId).ToList();
        var products = await db.Products.Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim()
        };

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return (false, "Sản phẩm không tồn tại.", null);
            }

            if (item.Quantity <= 0 || item.Quantity > product.Stock)
            {
                return (false, "Tồn kho không đủ.", null);
            }

            var unitPrice = product.SaleApproved && product.SalePrice.HasValue ? product.SalePrice.Value : product.Price;
            product.Stock -= item.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });
            order.Total += unitPrice * item.Quantity;
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return (true, "Đơn hàng đã được tạo.", order);
    }
}
