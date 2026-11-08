using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}
