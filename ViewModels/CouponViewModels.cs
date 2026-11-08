using Stockify.Models.Entities;
using Stockify.Models.Enums;

namespace Stockify.ViewModels;

public class CouponIndexViewModel
{
    public List<CouponSummaryViewModel> Coupons { get; set; } = new();
    public int TotalCouponsCount { get; set; }
    public int ActiveCouponsCount { get; set; }
    public decimal TotalSalesGenerated { get; set; }
    public decimal TotalDiscountsProvided { get; set; }
    public int TotalRedemptionsCount { get; set; }
}

public class CouponSummaryViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CouponDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? MaxUsageCount { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalSalesRevenue { get; set; }
    public decimal TotalDiscountGiven { get; set; }
    public int OrdersCount { get; set; }
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
}

public class CouponValidateRequest
{
    public string? Code { get; set; }
    public decimal Subtotal { get; set; }
}

