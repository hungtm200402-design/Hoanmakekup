using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Endpoints;

public static class AdminProductEndpoints
{
    public static async Task<IResult> GetAsync(BeautyDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.Products
            .OrderBy(product => product.Name)
            .Select(product => new
            {
                product.Id,
                product.Slug,
                product.Name,
                product.Price,
                product.SalePrice,
                product.SaleApproved,
                product.Stock,
                product.ImagePath,
                product.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    public static async Task<IResult> CreateAsync(UpsertProductRequest request, BeautyDbContext db, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var slug = NormalizeSlug(request.Slug);
        if (await db.Products.AnyAsync(product => product.Slug == slug, cancellationToken))
        {
            return Results.Conflict(new { error = "Slug sản phẩm đã tồn tại." });
        }

        var product = new Product
        {
            Slug = slug,
            Name = request.Name.Trim(),
            Price = request.Price,
            SalePrice = request.SalePrice,
            SaleApproved = request.SaleApproved && request.SalePrice.HasValue,
            Stock = request.Stock,
            ImagePath = request.ImagePath.Trim()
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/products/{product.Slug}", product);
    }

    public static async Task<IResult> UpdateAsync(Guid id, UpsertProductRequest request, BeautyDbContext db, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var slug = NormalizeSlug(request.Slug);
        if (await db.Products.AnyAsync(item => item.Id != id && item.Slug == slug, cancellationToken))
        {
            return Results.Conflict(new { error = "Slug sản phẩm đã tồn tại." });
        }

        product.Slug = slug;
        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.SalePrice = request.SalePrice;
        product.SaleApproved = request.SaleApproved && request.SalePrice.HasValue;
        product.Stock = request.Stock;
        product.ImagePath = request.ImagePath.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(product);
    }

    public static async Task<IResult> DeleteAsync(Guid id, BeautyDbContext db, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        if (await db.OrderItems.AnyAsync(item => item.ProductId == id, cancellationToken))
        {
            return Results.Conflict(new { error = "Không thể xoá sản phẩm đã có trong đơn hàng. Hãy đặt tồn kho về 0 nếu muốn ẩn/bỏ bán." });
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static string? Validate(UpsertProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.Name))
        {
            return "Vui lòng nhập slug và tên sản phẩm.";
        }
        if (request.Price <= 0)
        {
            return "Giá sản phẩm phải lớn hơn 0.";
        }
        if (request.SalePrice.HasValue && (request.SalePrice <= 0 || request.SalePrice >= request.Price))
        {
            return "Giá sale phải lớn hơn 0 và nhỏ hơn giá gốc.";
        }
        if (request.Stock < 0)
        {
            return "Tồn kho không được âm.";
        }
        if (string.IsNullOrWhiteSpace(request.ImagePath) || !request.ImagePath.StartsWith('/'))
        {
            return "Đường dẫn ảnh phải bắt đầu bằng '/'.";
        }
        return null;
    }
}
