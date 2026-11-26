using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Stockify.Common.Validation;
using Stockify.Models.Enums;

namespace Stockify.ViewModels.Products;

public class FragranceNoteOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public FragranceNoteCategory Category { get; set; }
}

public class FragranceNoteCategoryGroupViewModel
{
    public FragranceNoteCategory Category { get; set; }
    public string CategoryNameEn { get; set; } = string.Empty;
    public string CategoryNameAr { get; set; } = string.Empty;
    public List<FragranceNoteOptionViewModel> Notes { get; set; } = new();
}

public interface IFragranceProfileFormModel
{
    List<AudienceTarget> SelectedAudiences { get; set; }
    List<FragranceSeason> SelectedSeasons { get; set; }
    FragranceProjection? Projection { get; set; }
    string? InspiredBy { get; set; }
    List<int> SelectedFragranceNoteIds { get; set; }
    bool IsFragranceProfileApproved { get; set; }
    DateTime? FragranceProfileReviewedAt { get; }
    List<FragranceNoteCategoryGroupViewModel> AvailableNotesGrouped { get; set; }
}

public class ProductSizeInputViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم الحجم مطلوب.")]
    [StringLength(40)]
    public string SizeLabel { get; set; } = "100ml";

    [Range(0, 10000000.00, ErrorMessage = "صافي المنتج لا يمكن أن يكون سالباً.")]
    public decimal CostPrice { get; set; } = 0m;

    [Range(0, 10000000.00, ErrorMessage = "المكسب لا يمكن أن يكون سالباً.")]
    public decimal ProfitAmount { get; set; } = 0m;

    public decimal SellingPrice { get; set; } = 0m;
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
}

public interface IProductPricingFormModel
{
    decimal CostPrice { get; set; }
    decimal ProfitAmount { get; set; }
    decimal Price { get; set; }
    List<ProductSizeInputViewModel> Sizes { get; set; }
}

public class ProductCreateViewModel : IFragranceProfileFormModel, IProductPricingFormModel
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, ErrorMessage = "Product name cannot exceed 150 characters.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU code is required.")]
    [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "SKU can only contain letters, numbers, hyphens and underscores.")]
    [Display(Name = "SKU Code")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(200, ErrorMessage = "Fragrance tagline cannot exceed 200 characters.")]
    [Display(Name = "تركيز العطر / الجملة التعريفية (Fragrance Tagline / Concentration)")]
    public string? FragranceTagline { get; set; }

    [Range(0, 10000000.00, ErrorMessage = "Net cost must be zero or greater.")]
    [Display(Name = "صافي المنتج (Net Cost EGP)")]
    public decimal CostPrice { get; set; } = 0m;

    [Range(0, 10000000.00, ErrorMessage = "Profit amount must be zero or greater.")]
    [Display(Name = "المكسب (Profit EGP)")]
    public decimal ProfitAmount { get; set; } = 0m;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 10000000.00, ErrorMessage = "Selling price (Net Cost + Profit) must be greater than zero.")]
    [Display(Name = "Final Selling Price (EGP)")]
    public decimal Price { get; set; }

    public List<ProductSizeInputViewModel> Sizes { get; set; } = new();

    [Required(ErrorMessage = "Initial stock quantity is required.")]
    [Range(0, 1000000, ErrorMessage = "Stock quantity cannot be negative.")]
    [Display(Name = "Initial Stock Quantity")]
    public int StockQuantity { get; set; } = 0;

    [Required(ErrorMessage = "Reorder threshold is required.")]
    [Range(1, 10000, ErrorMessage = "Reorder threshold must be at least 1.")]
    [Display(Name = "Reorder Threshold")]
    public int ReorderLevel { get; set; } = 10;

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Categories (الأقسام والتصنيفات)")]
    public List<int> SelectedCategoryIds { get; set; } = new();

    [Display(Name = "Product Image")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Featured Product (عطر مميز)")]
    public bool IsFeatured { get; set; } = false;

    [Display(Name = "Best Seller (الأكثر طلباً)")]
    public bool IsBestSeller { get; set; } = false;

    public IEnumerable<SelectListItem> CategoriesList { get; set; } = new List<SelectListItem>();

    // Fragrance Discovery Profile (Find Your Scent ✨)
    [Display(Name = "Target Audience (الفئة المستهدفة)")]
    public List<AudienceTarget> SelectedAudiences { get; set; } = new();

    [ExclusiveAllYearSeason]
    [Display(Name = "Best Seasons (الفصول المناسبة)")]
    public List<FragranceSeason> SelectedSeasons { get; set; } = new();

    [Display(Name = "Projection / Sillage (قوة الفوحان والثبات)")]
    public FragranceProjection? Projection { get; set; }

    [StringLength(150, ErrorMessage = "Inspired By cannot exceed 150 characters.")]
    [Display(Name = "Inspired By (مستوحى من عطر عالمي)")]
    public string? InspiredBy { get; set; }

    [Display(Name = "Fragrance Notes (النوتات العطرية المكونة)")]
    public List<int> SelectedFragranceNoteIds { get; set; } = new();

    [Display(Name = "Approve for Recommendations (اعتماد العطر في نظام ترشيحات Find Your Scent)")]
    public bool IsFragranceProfileApproved { get; set; } = false;

    public DateTime? FragranceProfileReviewedAt => null;

    public List<FragranceNoteCategoryGroupViewModel> AvailableNotesGrouped { get; set; } = new();
}

public class ProductEditViewModel : IFragranceProfileFormModel, IProductPricingFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, ErrorMessage = "Product name cannot exceed 150 characters.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU code is required.")]
    [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters.")]
    [Display(Name = "SKU Code")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(200, ErrorMessage = "Fragrance tagline cannot exceed 200 characters.")]
    [Display(Name = "تركيز العطر / الجملة التعريفية (Fragrance Tagline / Concentration)")]
    public string? FragranceTagline { get; set; }

    [Range(0, 10000000.00, ErrorMessage = "Net cost must be zero or greater.")]
    [Display(Name = "صافي المنتج (Net Cost EGP)")]
    public decimal CostPrice { get; set; } = 0m;

    [Range(0, 10000000.00, ErrorMessage = "Profit amount must be zero or greater.")]
    [Display(Name = "المكسب (Profit EGP)")]
    public decimal ProfitAmount { get; set; } = 0m;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 10000000.00, ErrorMessage = "Selling price (Net Cost + Profit) must be greater than zero.")]
    [Display(Name = "Final Selling Price (EGP)")]
    public decimal Price { get; set; }

    public List<ProductSizeInputViewModel> Sizes { get; set; } = new();

    [Required(ErrorMessage = "Stock quantity is required.")]
    [Range(0, 1000000, ErrorMessage = "Stock quantity must be zero or greater.")]
    [Display(Name = "Current Stock Quantity")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Reorder threshold is required.")]
    [Range(1, 10000, ErrorMessage = "Reorder threshold must be at least 1.")]
    [Display(Name = "Reorder Threshold")]
    public int ReorderLevel { get; set; } = 10;

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Categories (الأقسام والتصنيفات)")]
    public List<int> SelectedCategoryIds { get; set; } = new();

    [Display(Name = "Product Status")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured Product (عطر مميز)")]
    public bool IsFeatured { get; set; } = false;

    [Display(Name = "Best Seller (الأكثر طلباً)")]
    public bool IsBestSeller { get; set; } = false;

    public string? ExistingImageUrl { get; set; }

    public bool RemoveImage { get; set; } = false;

    [Display(Name = "Replace Product Image")]
    public IFormFile? ImageFile { get; set; }

    public IEnumerable<SelectListItem> CategoriesList { get; set; } = new List<SelectListItem>();

    // Fragrance Discovery Profile (Find Your Scent ✨)
    [Display(Name = "Target Audience (الفئة المستهدفة)")]
    public List<AudienceTarget> SelectedAudiences { get; set; } = new();

    [ExclusiveAllYearSeason]
    [Display(Name = "Best Seasons (الفصول المناسبة)")]
    public List<FragranceSeason> SelectedSeasons { get; set; } = new();

    [Display(Name = "Projection / Sillage (قوة الفوحان والثبات)")]
    public FragranceProjection? Projection { get; set; }

    [StringLength(150, ErrorMessage = "Inspired By cannot exceed 150 characters.")]
    [Display(Name = "Inspired By (مستوحى من عطر عالمي)")]
    public string? InspiredBy { get; set; }

    [Display(Name = "Fragrance Notes (النوتات العطرية المكونة)")]
    public List<int> SelectedFragranceNoteIds { get; set; } = new();

    [Display(Name = "Approve for Recommendations (اعتماد العطر في نظام ترشيحات Find Your Scent)")]
    public bool IsFragranceProfileApproved { get; set; } = false;

    public DateTime? FragranceProfileReviewedAt { get; set; }

    public List<FragranceNoteCategoryGroupViewModel> AvailableNotesGrouped { get; set; } = new();
}
