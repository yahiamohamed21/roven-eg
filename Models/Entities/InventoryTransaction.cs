using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
}

