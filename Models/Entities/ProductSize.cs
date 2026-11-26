using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class ProductSize
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required]
    [StringLength(40)]
    public string SizeLabel { get; set; } = "100ml";

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProfitAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SellingPrice { get; set; } = 0m;

    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
}
