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

[Authorize]
public class AbandonedCheckoutsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AbandonedCheckoutsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /AbandonedCheckouts
    public async Task<IActionResult> Index(string? filter, string? search)
    {
        var query = _context.AbandonedCheckouts
            .Include(a => a.RecoveredOrder)
            .AsNoTracking();

        var cleanFilter = filter?.Trim().ToLower() ?? "pending";

        if (cleanFilter == "pending")
        {
            query = query.Where(a => !a.IsRecovered);
        }
        else if (cleanFilter == "recovered")
        {
            query = query.Where(a => a.IsRecovered);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.CustomerName.ToLower().Contains(s)
                                  || a.CustomerPhone.Contains(s)
                                  || (a.CustomerEmail != null && a.CustomerEmail.ToLower().Contains(s))
                                  || (a.City != null && a.City.ToLower().Contains(s))
                                  || a.CartItemsJson.ToLower().Contains(s));
            ViewBag.CurrentSearch = search;
        }

        ViewBag.CurrentFilter = cleanFilter;
        ViewBag.TotalPending = await _context.AbandonedCheckouts.CountAsync(a => !a.IsRecovered);
        ViewBag.TotalRecovered = await _context.AbandonedCheckouts.CountAsync(a => a.IsRecovered);
        ViewBag.TotalAll = await _context.AbandonedCheckouts.CountAsync();
        ViewBag.PendingValue = await _context.AbandonedCheckouts.Where(a => !a.IsRecovered).SumAsync(a => (decimal?)a.TotalAmount) ?? 0m;

        var results = await query
            .OrderByDescending(a => a.LastUpdatedAt)
            .ToListAsync();

        return View(results);
    }

    // POST: /AbandonedCheckouts/MarkRecovered/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRecovered(int id)
    {
        var record = await _context.AbandonedCheckouts.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        record.IsRecovered = true;
        record.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم تحديث حالة السلة المتروكة بنجاح وتصنيفها كطلب مسترجع.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /AbandonedCheckouts/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.AbandonedCheckouts.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        _context.AbandonedCheckouts.Remove(record);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم حذف سجل الطلب غير المكتمل بنجاح.";
        return RedirectToAction(nameof(Index));
    }
}

