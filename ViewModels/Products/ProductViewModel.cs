namespace Stockify.ViewModels.Products;

public class ProductViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal Price { get; set; }
    public List<string> SizeLabels { get; set; } = new();
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsTester { get; set; } = false;
    public int TesterRequiredCount { get; set; } = 4;
    public int TesterOptionsCount { get; set; } = 0;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> CategoryNames { get; set; } = new();

    public string StockStatus => StockQuantity switch
    {
        <= 0 => "Out of Stock",
        var q when q <= ReorderLevel => "Low Stock",
        _ => "In Stock"
    };

    public string StockBadgeClass => StockQuantity switch
    {
        <= 0 => "bg-danger text-white",
        var q when q <= ReorderLevel => "bg-warning-subtle text-warning border border-warning-subtle",
        _ => "bg-success-subtle text-success border border-success-subtle"
    };
}
