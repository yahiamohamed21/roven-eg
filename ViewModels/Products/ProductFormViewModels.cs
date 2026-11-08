using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Stockify.ViewModels.Products;

public class ProductCreateViewModel
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

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 10000000.00, ErrorMessage = "Price must be greater than zero.")]
    [Display(Name = "Selling Price (EGP)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Initial stock quantity is required.")]
    [Range(0, 1000000, ErrorMessage = "Stock quantity cannot be negative.")]
    [Display(Name = "Initial Stock Quantity")]
    public int StockQuantity { get; set; } = 0;

    [Required(ErrorMessage = "Reorder threshold is required.")]
    [Range(1, 10000, ErrorMessage = "Reorder threshold must be at least 1.")]
    [Display(Name = "Reorder Threshold")]
    public int ReorderLevel { get; set; } = 10;

    [Required(ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Product Image")]
    public IFormFile? ImageFile { get; set; }

    public IEnumerable<SelectListItem> CategoriesList { get; set; } = new List<SelectListItem>();
}

public class ProductEditViewModel
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

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 10000000.00, ErrorMessage = "Price must be greater than zero.")]
    [Display(Name = "Selling Price (EGP)")]
    public decimal Price { get; set; }

    [Display(Name = "Current Stock Quantity")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Reorder threshold is required.")]
    [Range(1, 10000, ErrorMessage = "Reorder threshold must be at least 1.")]
    [Display(Name = "Reorder Threshold")]
    public int ReorderLevel { get; set; } = 10;

    [Required(ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Product Status")]
    public bool IsActive { get; set; } = true;

    public string? ExistingImageUrl { get; set; }

    [Display(Name = "Replace Product Image")]
    public IFormFile? ImageFile { get; set; }

    public IEnumerable<SelectListItem> CategoriesList { get; set; } = new List<SelectListItem>();
}

