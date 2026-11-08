using System;

namespace Stockify.Models.Entities;

public class AbandonedCheckout
{
    public int Id { get; set; }
    public string? SessionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? ShippingAddress { get; set; }
    public string? City { get; set; }
    public string? OrderNotes { get; set; }
    public string CartItemsJson { get; set; } = "[]";
    public decimal TotalAmount { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal ShippingFee { get; set; } = 0;
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRecovered { get; set; } = false;
    public int? RecoveredOrderId { get; set; }
    public Order? RecoveredOrder { get; set; }
}

