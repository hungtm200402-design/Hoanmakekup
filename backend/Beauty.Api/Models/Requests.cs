namespace Beauty.Api.Models;

public sealed class CreateAppointmentRequest
{
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Service { get; set; } = "";
    public string Tone { get; set; } = "";
    public string Note { get; set; } = "";
    public string StartAt { get; set; } = "";
}

public sealed record CreateOrderRequest(
    string CustomerName,
    string Phone,
    string Address,
    List<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record CreateSaleRequest(Guid ProductId, decimal SalePrice);

public sealed record CreateAiDraftRequest(string Type, string Prompt, string SourceImagePrivatePath);

public sealed record ConfirmedProductRequest(
    string ProductName,
    string Brand,
    string ProductLine,
    string Variant,
    string Shade,
    string Finish,
    string Category,
    string ItemForm,
    string Size,
    string SearchQuery,
    bool UserConfirmed,
    string OfficialProductUrl,
    string Price,
    string SalePrice,
    string Gift,
    string ShopName,
    string Phone,
    string Address,
    string Website,
    string RemainingQuantity,
    bool IsRewrite,
    string PreviousCreativeDirection);

public sealed record VerifyProductUrlRequest(
    ConfirmedProductRequest Product,
    string Url);

public sealed record UpsertProductRequest(
    string Slug,
    string Name,
    decimal Price,
    decimal? SalePrice,
    bool SaleApproved,
    int Stock,
    string ImagePath);

public sealed record UpdateAppointmentStatusRequest(AppointmentStatus Status);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);

public sealed record ReviewAiDraftRequest(DraftStatus Status);

public sealed record CapturedProductSourceRequest(
    string SourceUrl,
    string CanonicalUrl,
    string DocumentTitle,
    string OgTitle,
    string OgImage,
    string SelectedImage,
    string ProductDataJson,
    string SourceDomain,
    string Brand,
    string ProductName);
