using System.ComponentModel.DataAnnotations;
using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class FragranceNote
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ArabicName { get; set; } = string.Empty;

    public FragranceNoteCategory Category { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductFragranceNote> ProductFragranceNotes { get; set; } = new List<ProductFragranceNote>();
    public ICollection<FragranceNoteQuickPick> QuickPicks { get; set; } = new List<FragranceNoteQuickPick>();
}
