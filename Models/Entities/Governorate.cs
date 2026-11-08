using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class Governorate
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم المحافظة بالعربية مطلوب")]
    [MaxLength(100)]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم المحافظة بالإنجليزية مطلوب")]
    [MaxLength(100)]
    public string NameEn { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 10000, ErrorMessage = "سعر الشحن يجب أن يكون بين 0 و 10,000 ج.م")]
    public decimal ShippingFee { get; set; } = 50.00m;

    [MaxLength(100)]
    public string? DeliveryTime { get; set; } = "1-3 أيام عمل";

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

