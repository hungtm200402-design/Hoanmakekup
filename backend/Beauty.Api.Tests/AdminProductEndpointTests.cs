using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AdminProductEndpointTests
{
    [Fact]
    public async Task CreateAsync_CreatesProductWithValidatedData()
    {
        await using var db = CreateDbContext();

        var result = await AdminProductEndpoints.CreateAsync(new UpsertProductRequest(
            "phan-nuoc-lumina",
            "Phấn nước Lumina",
            320000,
            280000,
            true,
            12,
            "/images/products/lumina.png"), db, CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        var saved = await db.Products.SingleAsync();
        Assert.Equal("phan-nuoc-lumina", saved.Slug);
        Assert.True(saved.SaleApproved);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidPrice()
    {
        await using var db = CreateDbContext();
        var product = new Product { Slug = "son-lumina", Name = "Son Lumina", Price = 180000, Stock = 4, ImagePath = "/images/products/lumina.png" };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var result = await AdminProductEndpoints.UpdateAsync(product.Id, new UpsertProductRequest(
            "son-lumina", "Son Lumina", 0, null, false, 4, "/images/products/lumina.png"), db, CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Giá sản phẩm", response.Body);
    }

    [Fact]
    public async Task DeleteAsync_RejectsProductAlreadyReferencedByAnOrder()
    {
        await using var db = CreateDbContext();
        var product = new Product { Slug = "son-lumina", Name = "Son Lumina", Price = 180000, Stock = 4, ImagePath = "/images/products/lumina.png" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.OrderItems.Add(new OrderItem { OrderId = Guid.NewGuid(), ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPrice = product.Price });
        await db.SaveChangesAsync();

        var result = await AdminProductEndpoints.DeleteAsync(product.Id, db, CancellationToken.None);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public void Program_ProtectsProductMutationsWithAdminPolicy()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Beauty.Api", "Program.cs"));

        Assert.Contains("MapPost(\"/api/admin/products\", AdminProductEndpoints.CreateAsync).RequireAuthorization(\"Admin\")", program);
        Assert.Contains("MapPut(\"/api/admin/products/{id:guid}\", AdminProductEndpoints.UpdateAsync).RequireAuthorization(\"Admin\")", program);
        Assert.Contains("MapDelete(\"/api/admin/products/{id:guid}\", AdminProductEndpoints.DeleteAsync).RequireAuthorization(\"Admin\")", program);
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static BeautyDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BeautyDbContext>()
        .UseInMemoryDatabase($"AdminProductEndpointTests-{Guid.NewGuid():N}")
        .Options);
}
