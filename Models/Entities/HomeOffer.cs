using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stockify.Models.Entities;

public class HomeOffer
{
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>
    /// اختياري: عطر ثاني في حال كان العرض باقة / دويتو يحتوي على عطرين معاً
    /// </summary>
    public int? SecondProductId { get; set; }

    public Product? SecondProduct { get; set; }

    /// <summary>
    /// اختياري: عطر ثالث في حال كان العرض باقة ثلاثية تحتوي على 3 عطور
    /// </summary>
    public int? ThirdProductId { get; set; }

    public Product? ThirdProduct { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(150)]
    public string? BadgeText { get; set; } = "EXCLUSIVE OFFER • عرض خاص";

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 100000, ErrorMessage = "سعر العرض يجب أن يكون بين 0 و 100,000 ج.م")]
    public decimal OfferPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalPriceOverride { get; set; }

    [MaxLength(500)]
    public string? CustomImageUrl { get; set; }

    [MaxLength(1000)]
    public string? CustomDescription { get; set; }

    [MaxLength(100)]
    public string? ButtonText { get; set; } = "SHOP THE SET";

    [MaxLength(300)]
    public string? FeaturePills { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(0, 100)]
    public int AllocatedPercent { get; set; } = 82;

    [Range(0, 10000)]
    public int RemainingCount { get; set; } = 25;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

