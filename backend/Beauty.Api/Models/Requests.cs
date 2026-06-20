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

public sealed record CreateSaleRequest(Guid ProductId, decimal SalePrice);

public sealed record CreateAiDraftRequest(string Type, string Prompt, string SourceImagePrivatePath);
