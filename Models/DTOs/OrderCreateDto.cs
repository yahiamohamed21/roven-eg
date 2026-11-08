namespace Stockify.Models.DTOs;

public class OrderCreateDto
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? City { get; set; }
    public string? ShippingAddress { get; set; }
    public string? CouponCode { get; set; }
    public string PaymentMethod { get; set; } = "Cash on Delivery";
    public string? Notes { get; set; }
    public List<OrderItemCreateDto> Items { get; set; } = new();
}

public class OrderItemCreateDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

