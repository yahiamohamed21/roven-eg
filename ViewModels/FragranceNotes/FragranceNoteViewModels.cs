using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Stockify.Models.Enums;

namespace Stockify.ViewModels.FragranceNotes;

public class FragranceNoteListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public FragranceNoteCategory Category { get; set; }
    public string CategoryNameEn { get; set; } = string.Empty;
    public string CategoryNameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsLoveQuickPick { get; set; }
    public bool IsAvoidQuickPick { get; set; }
    public int ProductsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FragranceNoteIndexViewModel
{
    public List<FragranceNoteListItemViewModel> Notes { get; set; } = new();
    public int? SelectedCategory { get; set; }
    public string? SearchTerm { get; set; }
    public bool? ActiveOnly { get; set; }

    public int TotalNotesCount { get; set; }
    public int ActiveNotesCount { get; set; }
    public int LoveQuickPicksCount { get; set; }
    public int AvoidQuickPicksCount { get; set; }
}

public class FragranceNoteFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "English name is required.")]
    [StringLength(100, ErrorMessage = "English name cannot exceed 100 characters.")]
    [Display(Name = "English Name (الاسم بالإنجليزية)")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Arabic name is required.")]
    [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters.")]
    [Display(Name = "Arabic Name (الاسم بالعربية)")]
    public string ArabicName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a fragrance category.")]
    [Display(Name = "Olfactive Category (عائلة النوتة العطرية)")]
    public FragranceNoteCategory Category { get; set; }

    [Display(Name = "Active in Catalog (نشط في الكتالوج)")]
    public bool IsActive { get; set; } = true;

    [Range(0, 10000, ErrorMessage = "Display order must be zero or positive.")]
    [Display(Name = "Display Order (ترتيب الظهور)")]
    public int DisplayOrder { get; set; } = 0;

    [Display(Name = "Quick Pick for Loved Notes (سؤال 2: النوتات المحبوبة)")]
    public bool IsLoveQuickPick { get; set; } = false;

    [Display(Name = "Quick Pick for Avoided Notes (سؤال 3: النوتات غير المرغوبة)")]
    public bool IsAvoidQuickPick { get; set; } = false;

    public int AssignedProductsCount { get; set; } = 0;
    public List<string> AssignedProductNames { get; set; } = new();
}

public class QuickPicksManagementViewModel
{
    public QuickPickContext Context { get; set; }
    public string ContextTitle { get; set; } = string.Empty;
    public string ContextDescription { get; set; } = string.Empty;
    public List<QuickPickItemViewModel> QuickPicks { get; set; } = new();
    public List<SelectListItem> AvailableNotesSelectList { get; set; } = new();
    public int? SelectedNoteIdToAdd { get; set; }
}

public class QuickPickItemViewModel
{
    public int FragranceNoteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public FragranceNoteCategory Category { get; set; }
    public int DisplayOrder { get; set; }
}
