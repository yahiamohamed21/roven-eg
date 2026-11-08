using Stockify.Models.Entities;
using Stockify.Models.Enums;

namespace Stockify.Services.Interfaces;

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest
{
    public OrderSource OrderSource { get; set; } = OrderSource.Website;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? ShippingAddress { get; set; }
    public string? City { get; set; }
    public string? OrderNotes { get; set; }
    public string? CouponCode { get; set; }
    public decimal ShippingFee { get; set; } = 0;
    public string PaymentMethod { get; set; } = "Cash on Delivery";
    public string? UserId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? OutOfStockProductId { get; set; }
    public string? OutOfStockProductName { get; set; }
    public int? AvailableStock { get; set; }
    public Order? Order { get; set; }
    public string? OrderNumber => Order?.OrderNumber;
}

public interface IOrderService
{
    /// <summary>
    /// Atomically validates, creates an order, and deducts inventory using direct SQL concurrency locking.
    /// Eliminates Race Conditions (Lost Updates / Overselling) between concurrent Website customers and Sales POS users.
    /// </summary>
    Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request);

    /// <summary>
    /// Retrieves order details along with items and customer information.
    /// </summary>
    Task<Order?> GetOrderByNumberAsync(string orderNumber);
}
