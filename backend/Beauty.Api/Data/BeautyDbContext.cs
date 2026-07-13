using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Data;

public sealed class BeautyDbContext(DbContextOptions<BeautyDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AiDraft> AiDrafts => Set<AiDraft>();
    public DbSet<TrustedSourceDomain> TrustedSourceDomains => Set<TrustedSourceDomain>();
    public DbSet<TrustedProduct> TrustedProducts => Set<TrustedProduct>();
    public DbSet<TrustedProductImage> TrustedProductImages => Set<TrustedProductImage>();
    public DbSet<IndexingJob> IndexingJobs => Set<IndexingJob>();
    public DbSet<CapturedProductSource> CapturedProductSources => Set<CapturedProductSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(product => product.Slug).IsUnique();
        modelBuilder.Entity<Product>().Property(product => product.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Product>().Property(product => product.SalePrice).HasPrecision(12, 2);
        modelBuilder.Entity<Order>().Property(order => order.Total).HasPrecision(12, 2);
        modelBuilder.Entity<OrderItem>().Property(item => item.UnitPrice).HasPrecision(12, 2);
        modelBuilder.Entity<Appointment>().HasIndex(appointment => new { appointment.StartAt, appointment.EndAt });
        modelBuilder.Entity<Order>().HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId);
        modelBuilder.Entity<TrustedSourceDomain>().HasIndex(item => item.Domain).IsUnique();
        modelBuilder.Entity<TrustedProduct>().HasIndex(item => item.CanonicalUrl).IsUnique();
        modelBuilder.Entity<TrustedProduct>().HasIndex(item => item.NormalizedKey);
        modelBuilder.Entity<TrustedProductImage>().HasIndex(item => item.ImageUrl).IsUnique();
        modelBuilder.Entity<CapturedProductSource>().HasIndex(item => item.ExactImageHash);
        modelBuilder.Entity<CapturedProductSource>().HasIndex(item => item.CanonicalUrl);
        modelBuilder.Entity<CapturedProductSource>().HasIndex(item => item.ImageUrl);
        modelBuilder.Entity<TrustedProductImage>()
            .HasOne(item => item.Product)
            .WithMany(item => item.Images)
            .HasForeignKey(item => item.TrustedProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Slug = "son-black-rouge-air-fit",
                Name = "Son Black Rouge Air Fit",
                Price = 280000,
                SalePrice = 250000,
                SaleApproved = true,
                Stock = 89,
                ImagePath = "/images/products/black-rouge.png"
            },
            new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Slug = "perfect-diary",
                Name = "Kem nền Perfect Diary",
                Price = 350000,
                Stock = 67,
                ImagePath = "/images/products/perfect-diary.png"
            }
        );
    }
}
