namespace Stockify.ViewModels.Store;

public class AbandonedCheckoutDraftDto
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? ShippingAddress { get; set; }
    public string? City { get; set; }
    public string? OrderNotes { get; set; }
}

