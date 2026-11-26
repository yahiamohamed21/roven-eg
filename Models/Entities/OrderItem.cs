using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? ProductSizeId { get; set; }

    [StringLength(40)]
    public string? SizeLabel { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitProfit { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal TotalPrice => Quantity * UnitPrice;

    [NotMapped]
    public decimal TotalCost => Quantity * UnitCost;

    [NotMapped]
    public decimal TotalProfit => Quantity * UnitProfit;
}
