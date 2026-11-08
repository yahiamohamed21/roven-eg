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

    public int ProductsCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
