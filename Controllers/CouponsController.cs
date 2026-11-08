using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CouponsController : Controller
{
    private readonly ApplicationDbContext _context;

    public CouponsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Coupons
    public async Task<IActionResult> Index()
    {
        var coupons = await _context.Coupons
            .Include(c => c.Orders)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var summaries = coupons.Select(c =>
        {
            var validOrders = c.Orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
            return new CouponSummaryViewModel
            {
                Id = c.Id,
                Code = c.Code,
                Description = c.Description,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MinimumOrderAmount = c.MinimumOrderAmount,
                MaxDiscountAmount = c.MaxDiscountAmount,
                ExpiryDate = c.ExpiryDate,
                MaxUsageCount = c.MaxUsageCount,
                TimesUsed = c.TimesUsed,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                TotalSalesRevenue = validOrders.Sum(o => o.TotalAmount),
                TotalDiscountGiven = validOrders.Sum(o => o.DiscountAmount),
                OrdersCount = validOrders.Count
            };
        }).ToList();

        var model = new CouponIndexViewModel
        {
            Coupons = summaries,
            TotalCouponsCount = summaries.Count,
            ActiveCouponsCount = summaries.Count(c => c.IsActive && !c.IsExpired),
            TotalSalesGenerated = summaries.Sum(c => c.TotalSalesRevenue),
            TotalDiscountsProvided = summaries.Sum(c => c.TotalDiscountGiven),
            TotalRedemptionsCount = summaries.Sum(c => c.TimesUsed)
        };

        return View(model);
    }

    // POST: /Coupons/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Coupon model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            TempData["ErrorMessage"] = "كود الكوبون مطلوب.";
            return RedirectToAction(nameof(Index));
        }

        var cleanCode = model.Code.Trim().ToUpper();
        var exists = await _context.Coupons.AnyAsync(c => c.Code == cleanCode);
        if (exists)
        {
            TempData["ErrorMessage"] = $"الكود '{cleanCode}' مسجل مسبقاً في النظام. يرجى اختيار كود فريد.";
            return RedirectToAction(nameof(Index));
        }

        if (model.DiscountValue <= 0)
        {
            TempData["ErrorMessage"] = "قيمة الخصم يجب أن تكون أكبر من الصفر.";
            return RedirectToAction(nameof(Index));
        }

        if (model.DiscountType == CouponDiscountType.Percentage && model.DiscountValue > 100)
        {
            TempData["ErrorMessage"] = "نسبة الخصم لا يمكن أن تتجاوز 100%.";
            return RedirectToAction(nameof(Index));
        }

        model.Code = cleanCode;
        model.CreatedAt = DateTime.UtcNow;
        model.TimesUsed = 0;

        _context.Coupons.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم إضافة الكوبون الفاخر '{cleanCode}' بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Coupons/ToggleActive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        coupon.IsActive = !coupon.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = coupon.IsActive
            ? $"تم تفعيل الكوبون '{coupon.Code}' بنجاح."
            : $"تم تعطيل الكوبون '{coupon.Code}' مؤقتاً.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Coupons/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await _context.Coupons.Include(c => c.Orders).FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
        {
            return NotFound();
        }

        if (coupon.Orders.Any())
        {
            coupon.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["WarningMessage"] = $"الكوبون '{coupon.Code}' مرتبط بطلبات شراء سابقة، لذلك تم تعطيله لحفظ السجلات المالية بدلاً من حذفه.";
            return RedirectToAction(nameof(Index));
        }

        _context.Coupons.Remove(coupon);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم حذف الكوبون '{coupon.Code}' بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Coupons/Orders/5
    public async Task<IActionResult> Orders(int id)
    {
        var coupon = await _context.Coupons
            .Include(c => c.Orders)
                .ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (coupon == null)
        {
            return NotFound();
        }

        return View(coupon);
    }

    // POST: /Coupons/Validate (Public / Sales Accessible Endpoint)
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromBody] CouponValidateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
        {
            return Json(new { success = false, message = "يرجى إدخال كود الكوبون." });
        }

        var cleanCode = request.Code.Trim().ToUpper();
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == cleanCode);

        if (coupon == null)
        {
            return Json(new { success = false, message = "كود الكوبون غير صحيح أو غير موجود." });
        }

        if (!coupon.IsActive)
        {
            return Json(new { success = false, message = "هذا الكوبون غير فعال حالياً." });
        }

        var now = DateTime.UtcNow;
        if (coupon.StartDate.HasValue && coupon.StartDate.Value > now)
        {
            return Json(new { success = false, message = "لم يبدأ موعد سريان هذا الكوبون بعد." });
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < now)
        {
            return Json(new { success = false, message = "عذراً، هذا الكوبون منتهي الصلاحية." });
        }

        if (coupon.MaxUsageCount.HasValue && coupon.TimesUsed >= coupon.MaxUsageCount.Value)
        {
            return Json(new { success = false, message = "تم استنفاد الحد الأقصى لمرات استخدام هذا الكوبون." });
        }

        if (coupon.MinimumOrderAmount.HasValue && request.Subtotal < coupon.MinimumOrderAmount.Value)
        {
            return Json(new
            {
                success = false,
                message = $"الحد الأدنى لقيمة الطلب لتطبيق هذا الكوبون هو {coupon.MinimumOrderAmount.Value:N2} ج.م."
            });
        }

        decimal discount = 0;
        if (coupon.DiscountType == CouponDiscountType.Percentage)
        {
            discount = Math.Round((request.Subtotal * coupon.DiscountValue) / 100m, 2);
            if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
            {
                discount = coupon.MaxDiscountAmount.Value;
            }
        }
        else
        {
            discount = Math.Min(request.Subtotal, coupon.DiscountValue);
        }

        decimal newTotal = Math.Max(0m, request.Subtotal - discount);

        return Json(new
        {
            success = true,
            code = coupon.Code,
            discountType = coupon.DiscountType.ToString(),
            discountValue = coupon.DiscountValue,
            discountAmount = discount,
            newTotal = newTotal,
            message = coupon.DiscountType == CouponDiscountType.Percentage
                ? $"تم تطبيق خصم {coupon.DiscountValue}% بقيمة {discount:N2} ج.م بنجاح!"
                : $"تم تطبيق خصم بقيمة {discount:N2} ج.م بنجاح!"
        });
    }
}

