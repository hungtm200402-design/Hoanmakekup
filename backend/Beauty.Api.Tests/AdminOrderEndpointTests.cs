using System.Text;
using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AdminOrderEndpointTests
{
    [Fact]
    public async Task UpdateStatusAsync_PersistsAllowedOrderStatusTransition()
    {
        await using var db = CreateDbContext();
        var order = new Order { CustomerName = "Nguyen Thi Hoa", Phone = "0909000111", Address = "Ha Noi" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await AdminOrderEndpoints.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatus.Paid),
            db,
            CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(OrderStatus.Paid, (await db.Orders.FindAsync(order.Id))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsTransitionFromCompletedOrder()
    {
        await using var db = CreateDbContext();
        var order = new Order
        {
            CustomerName = "Nguyen Thi Hoa",
            Phone = "0909000111",
            Address = "Ha Noi",
            Status = OrderStatus.Completed
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await AdminOrderEndpoints.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatus.Shipping),
            db,
            CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Không thể chuyển trạng thái", response.Body);
    }

    [Fact]
    public async Task UpdateStatusAsync_RestoresStockWhenCancellingOrder()
    {
        await using var db = CreateDbContext();
        var product = new Product { Slug = "son-lumina", Name = "Son Lumina", Price = 180000, Stock = 3, ImagePath = "/images/products/lumina.png" };
        var order = new Order { CustomerName = "Nguyen Thi Hoa", Phone = "0909000111", Address = "Ha Noi" };
        order.Items.Add(new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = 2, UnitPrice = product.Price });
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var before = product.Stock;

        var result = await AdminOrderEndpoints.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(OrderStatus.Cancelled),
            db,
            CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(before + 2, (await db.Products.FindAsync(product.Id))!.Stock);
    }

    [Fact]
    public void Program_ProtectsAdminOrderStatusEndpointWithStaffPolicy()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Beauty.Api", "Program.cs"));

        Assert.Contains("MapPut(\"/api/admin/orders/{id:guid}/status\", AdminOrderEndpoints.UpdateStatusAsync).RequireAuthorization(\"Staff\")", program);
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static BeautyDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BeautyDbContext>()
        .UseInMemoryDatabase($"AdminOrderEndpointTests-{Guid.NewGuid():N}")
        .Options);
}
