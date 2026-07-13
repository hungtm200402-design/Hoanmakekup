namespace Beauty.Api.Models;

public enum UserRole
{
    Customer,
    Staff,
    Admin
}

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Rejected,
    Completed,
    Cancelled
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipping,
    Completed,
    Cancelled
}

public enum DraftStatus
{
    Draft,
    Approved,
    Rejected
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Customer;
}

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public bool SaleApproved { get; set; }
    public int Stock { get; set; }
    public string ImagePath { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Service { get; set; } = "";
    public string Tone { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? PrivateCustomerImagePath { get; set; }
}

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class AiDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public string SourceImagePrivatePath { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Content { get; set; } = "";
    public DraftStatus Status { get; set; } = DraftStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrustedSourceDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Domain { get; set; } = "";
    public string Brand { get; set; } = "";
    public string SourceType { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastIndexedAt { get; set; }
    public string LastStatus { get; set; } = "pending";
    public string LastError { get; set; } = "";
}

public sealed class TrustedProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Brand { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductLine { get; set; } = "";
    public string Category { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Shade { get; set; } = "";
    public string Size { get; set; } = "";
    public string ItemForm { get; set; } = "";
    public string Description { get; set; } = "";
    public string Ingredients { get; set; } = "";
    public string Usage { get; set; } = "";
    public string CanonicalUrl { get; set; } = "";
    public string SourceDomain { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string NormalizedKey { get; set; } = "";
    public DateTimeOffset LastIndexedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TrustedProductImage> Images { get; set; } = new();
}

public sealed class TrustedProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrustedProductId { get; set; }
    public TrustedProduct? Product { get; set; }
    public string ImageUrl { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset LastIndexedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class IndexingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Scope { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int DomainsScanned { get; set; }
    public int ProductsIndexed { get; set; }
    public int ImagesIndexed { get; set; }
    public string Error { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class CapturedProductSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExactImageHash { get; set; } = "";
    public string PerceptualHash { get; set; } = "";
    public string ImageEmbedding { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string CanonicalUrl { get; set; } = "";
    public string Brand { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductDataJson { get; set; } = "";
    public string SourceDomain { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
