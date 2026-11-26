using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FragranceTagline { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProfitAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public bool IsBestSeller { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Fragrance Discovery Profile (Find Your Scent)
    public FragranceProjection? Projection { get; set; }

    [StringLength(150)]
    public string? InspiredBy { get; set; }

    public bool IsFragranceProfileApproved { get; set; } = false;
    public DateTime? FragranceProfileReviewedAt { get; set; }

    // Tester / Discovery Pack Configuration
    public bool IsTester { get; set; } = false;
    public int TesterRequiredCount { get; set; } = 4;

    // Catalog & Admin Display Ordering
    public int DisplayOrder { get; set; } = 0;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    public ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
    public ICollection<ProductTesterOption> TesterOptions { get; set; } = new List<ProductTesterOption>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();

    // Fragrance Discovery Navigations
    public ICollection<ProductTargetAudience> TargetAudiences { get; set; } = new List<ProductTargetAudience>();
    public ICollection<ProductSeason> Seasons { get; set; } = new List<ProductSeason>();
    public ICollection<ProductFragranceNote> ProductFragranceNotes { get; set; } = new List<ProductFragranceNote>();
}
