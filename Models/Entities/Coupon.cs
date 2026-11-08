using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class Coupon
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public CouponDiscountType DiscountType { get; set; } = CouponDiscountType.Percentage;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinimumOrderAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxDiscountAmount { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public int? MaxUsageCount { get; set; }
    public int TimesUsed { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

