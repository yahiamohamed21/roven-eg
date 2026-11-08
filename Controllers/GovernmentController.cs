using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("goverment")]
[Route("Government")]
public class GovernmentController : Controller
{
    private readonly ApplicationDbContext _context;

    public GovernmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /goverment
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _context.Governorates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(g => g.NameAr.Contains(s) || g.NameEn.ToLower().Contains(s));
            ViewBag.Search = search;
        }

        if (status == "active")
        {
            query = query.Where(g => g.IsActive);
            ViewBag.Status = "active";
        }
        else if (status == "inactive")
        {
            query = query.Where(g => !g.IsActive);
            ViewBag.Status = "inactive";
        }

        var list = await query
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.NameAr)
            .ToListAsync();

        // Calculate KPI stats
        var allGovs = await _context.Governorates.AsNoTracking().ToListAsync();
        ViewBag.TotalCount = allGovs.Count;
        ViewBag.ActiveCount = allGovs.Count(g => g.IsActive);
        ViewBag.AvgFee = allGovs.Any() ? allGovs.Average(g => g.ShippingFee) : 0m;
        ViewBag.MinFee = allGovs.Any() ? allGovs.Min(g => g.ShippingFee) : 0m;
        ViewBag.MaxFee = allGovs.Any() ? allGovs.Max(g => g.ShippingFee) : 0m;

        return View("~/Views/Government/Index.cshtml", list);
    }

    // POST: /goverment/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Governorate model)
    {
        if (string.IsNullOrWhiteSpace(model.NameAr) || string.IsNullOrWhiteSpace(model.NameEn))
        {
            TempData["ErrorMessage"] = "يرجى إدخال اسم المحافظة بالعربية والإنجليزية.";
            return RedirectToAction(nameof(Index));
        }

        if (model.ShippingFee < 0)
        {
            TempData["ErrorMessage"] = "سعر الشحن لا يمكن أن يكون سالباً.";
            return RedirectToAction(nameof(Index));
        }

        model.NameAr = model.NameAr.Trim();
        model.NameEn = model.NameEn.Trim();
        model.DeliveryTime = string.IsNullOrWhiteSpace(model.DeliveryTime) ? "1-3 أيام عمل" : model.DeliveryTime.Trim();
        model.CreatedAt = DateTime.UtcNow;

        _context.Governorates.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تمت إضافة محافظة '{model.NameAr}' بنجاح بسعر شحن {model.ShippingFee:N2} ج.م!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /goverment/Edit
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Governorate model)
    {
        var existing = await _context.Governorates.FindAsync(model.Id);
        if (existing == null)
        {
            TempData["ErrorMessage"] = "المحافظة المطلوبة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(model.NameAr) || string.IsNullOrWhiteSpace(model.NameEn))
        {
            TempData["ErrorMessage"] = "اسم المحافظة بالعربية والإنجليزية مطلوب.";
            return RedirectToAction(nameof(Index));
        }

        existing.NameAr = model.NameAr.Trim();
        existing.NameEn = model.NameEn.Trim();
        existing.ShippingFee = Math.Max(0m, model.ShippingFee);
        existing.DeliveryTime = model.DeliveryTime?.Trim() ?? "1-3 أيام عمل";
        existing.IsActive = model.IsActive;
        existing.DisplayOrder = model.DisplayOrder;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم تحديث بيانات وسعر شحن محافظة '{existing.NameAr}' بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /goverment/QuickUpdatePrice
    [HttpPost("QuickUpdatePrice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickUpdatePrice(int id, decimal shippingFee)
    {
        var gov = await _context.Governorates.FindAsync(id);
        if (gov == null)
        {
            return Json(new { success = false, message = "المحافظة غير موجودة." });
        }

        gov.ShippingFee = Math.Max(0m, shippingFee);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            id = gov.Id,
            shippingFee = gov.ShippingFee,
            message = $"تم تحديث سعر الشحن لمحافظة {gov.NameAr} إلى {gov.ShippingFee:N2} ج.م"
        });
    }

    // POST: /goverment/ToggleStatus
    [HttpPost("ToggleStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var gov = await _context.Governorates.FindAsync(id);
        if (gov == null)
        {
            TempData["ErrorMessage"] = "المحافظة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        gov.IsActive = !gov.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم {(gov.IsActive ? "تفعيل" : "تعطيل")} محافظة '{gov.NameAr}' بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /goverment/Delete
    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var gov = await _context.Governorates.FindAsync(id);
        if (gov == null)
        {
            TempData["ErrorMessage"] = "المحافظة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        var name = gov.NameAr;
        _context.Governorates.Remove(gov);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم حذف محافظة '{name}' بنجاح من قائمة الشحن.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /goverment/rates (Public/internal API for live shipping lookup)
    [AllowAnonymous]
    [HttpGet("rates")]
    public async Task<IActionResult> GetRates()
    {
        var rates = await _context.Governorates
            .Where(g => g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.NameAr)
            .Select(g => new
            {
                id = g.Id,
                nameAr = g.NameAr,
                nameEn = g.NameEn,
                shippingFee = g.ShippingFee,
                deliveryTime = g.DeliveryTime
            })
            .ToListAsync();

        return Json(new { success = true, rates });
    }
}

