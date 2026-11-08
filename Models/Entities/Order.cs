using System.ComponentModel.DataAnnotations.Schema;
using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public OrderSource OrderSource { get; set; } = OrderSource.Manual;
    public string? ShippingAddress { get; set; }
    public string? City { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? OrderNotes { get; set; }
    public string PaymentMethod { get; set; } = "Cash on Delivery";

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public string? CouponCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingFee { get; set; } = 0;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
