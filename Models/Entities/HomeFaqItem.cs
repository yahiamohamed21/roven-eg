using System;
using System.ComponentModel.DataAnnotations;

namespace Stockify.Models.Entities;

public class HomeFaqItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نص السؤال مطلوب")]
    [MaxLength(400)]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "نص الإجابة مطلوب")]
    public string Answer { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? IconClass { get; set; } = "fa-solid fa-circle-question";

    public int DisplayOrder { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
