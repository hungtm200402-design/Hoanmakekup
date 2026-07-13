using Beauty.Api.Data;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesOrderFromMultipleProductsAndUpdatesStock()
    {
        await using var db = CreateDbContext();
        var lipstick = new Product
        {
            Slug = "son-black-rouge-air-fit",
            Name = "Son Black Rouge Air Fit",
            Price = 280000,
            SalePrice = 250000,
            SaleApproved = true,
            Stock = 5,
            ImagePath = "/images/products/black-rouge.png"
        };
        var foundation = new Product
        {
            Slug = "perfect-diary",
            Name = "Kem nền Perfect Diary",
            Price = 350000,
            Stock = 4,
            ImagePath = "/images/products/perfect-diary.png"
        };
        db.Products.AddRange(lipstick, foundation);
        await db.SaveChangesAsync();
        var service = new OrderService(db);

        var result = await service.CreateAsync(new CreateOrderRequest(
            "Nguyen Thi Hoa",
            "0909000111",
            "123 Nguyen Trai",
            [
                new CreateOrderItemRequest(lipstick.Id, 2),
                new CreateOrderItemRequest(foundation.Id, 1)
            ]), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.Order);
        Assert.Equal(850000, result.Order.Total);
        Assert.Equal(2, result.Order.Items.Count);
        Assert.Equal(250000, result.Order.Items.Single(item => item.ProductId == lipstick.Id).UnitPrice);
        Assert.Equal(350000, result.Order.Items.Single(item => item.ProductId == foundation.Id).UnitPrice);
        Assert.Equal(3, lipstick.Stock);
        Assert.Equal(3, foundation.Stock);
        Assert.Equal(1, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsOrderWhenStockIsNotEnough()
    {
        await using var db = CreateDbContext();
        var product = new Product
        {
            Slug = "son-black-rouge-air-fit",
            Name = "Son Black Rouge Air Fit",
            Price = 280000,
            Stock = 1,
            ImagePath = "/images/products/black-rouge.png"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var service = new OrderService(db);

        var result = await service.CreateAsync(new CreateOrderRequest(
            "Nguyen Thi Hoa",
            "0909000111",
            "123 Nguyen Trai",
            [new CreateOrderItemRequest(product.Id, 2)]), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal("Tồn kho không đủ.", result.Message);
        Assert.Null(result.Order);
        Assert.Equal(1, product.Stock);
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Theory]
    [InlineData("", "0909000111", "123 Nguyen Trai")]
    [InlineData("Nguyen Thi Hoa", "", "123 Nguyen Trai")]
    [InlineData("Nguyen Thi Hoa", "0909000111", "")]
    public async Task CreateAsync_RejectsMissingShippingInformation(string customerName, string phone, string address)
    {
        await using var db = CreateDbContext();
        var service = new OrderService(db);

        var result = await service.CreateAsync(new CreateOrderRequest(
            customerName,
            phone,
            address,
            [new CreateOrderItemRequest(Guid.NewGuid(), 1)]), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal("Vui lòng nhập đầy đủ tên, số điện thoại và địa chỉ.", result.Message);
        Assert.Null(result.Order);
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyCart()
    {
        await using var db = CreateDbContext();
        var service = new OrderService(db);

        var result = await service.CreateAsync(new CreateOrderRequest(
            "Nguyen Thi Hoa",
            "0909000111",
            "123 Nguyen Trai",
            []), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal("Đơn hàng phải có sản phẩm.", result.Message);
        Assert.Null(result.Order);
    }

    private static BeautyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BeautyDbContext>()
            .UseInMemoryDatabase($"OrderServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new BeautyDbContext(options);
    }
}
