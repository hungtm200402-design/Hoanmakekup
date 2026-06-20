using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Data;

public sealed class DatabaseBootstrapper(IServiceProvider services, ILogger<DatabaseBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BeautyDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await SeedProductsAsync(db, cancellationToken);
            logger.LogInformation("Database schema is ready.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database is not ready. API health remains available; DB endpoints need PostgreSQL connection.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedProductsAsync(BeautyDbContext db, CancellationToken cancellationToken)
    {
        var products = new[]
        {
            new Product { Slug = "perfect-diary", Name = "Kem nền Perfect Diary", Price = 350000, Stock = 67, ImagePath = "/images/products/perfect-diary.png" },
            new Product { Slug = "son-black-rouge-air-fit", Name = "Son Black Rouge Air Fit", Price = 280000, SalePrice = 250000, SaleApproved = true, Stock = 89, ImagePath = "/images/products/black-rouge.png" },
            new Product { Slug = "innisfree-powder", Name = "Phấn phủ Innisfree", Price = 320000, Stock = 45, ImagePath = "/images/products/innisfree-powder.png" },
            new Product { Slug = "the-ordinary-serum", Name = "Serum The Ordinary", Price = 310000, SalePrice = 280000, SaleApproved = true, Stock = 38, ImagePath = "/images/products/ordinary-serum.png" },
            new Product { Slug = "maybelline-mascara", Name = "Mascara Maybelline", Price = 220000, Stock = 52, ImagePath = "/images/products/maybelline-mascara.png" },
            new Product { Slug = "bioderma", Name = "Tẩy trang Bioderma", Price = 350000, Stock = 58, ImagePath = "/images/products/bioderma.png" }
        };

        foreach (var product in products)
        {
            var existing = await db.Products.FirstOrDefaultAsync(item => item.Slug == product.Slug, cancellationToken);
            if (existing is null)
            {
                db.Products.Add(product);
                continue;
            }

            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.SalePrice = product.SalePrice;
            existing.SaleApproved = product.SaleApproved;
            existing.ImagePath = product.ImagePath;
            if (existing.Stock <= 0)
            {
                existing.Stock = product.Stock;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
