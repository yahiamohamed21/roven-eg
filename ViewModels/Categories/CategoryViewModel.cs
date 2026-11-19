using System.ComponentModel.DataAnnotations;

namespace Stockify.ViewModels.Categories;

public class CategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Category Image URL")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Upload Category Image")]
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }

    public bool RemoveImage { get; set; } = false;

    public int ProductsCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
