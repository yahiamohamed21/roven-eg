using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _context;

    public MessagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Messages
    public async Task<IActionResult> Index(string? search = null, string? status = null, string? tab = null, string? reviewStatus = null)
    {
        var query = _context.ContactMessages.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(m => m.FullName.Contains(s) || m.PhoneNumber.Contains(s) || (m.Email != null && m.Email.Contains(s)) || m.Message.Contains(s));
        }

        if (status == "unread")
        {
            query = query.Where(m => !m.IsRead);
        }
        else if (status == "read")
        {
            query = query.Where(m => m.IsRead);
        }

        var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();

        // Product Reviews Query
        var reviewsQuery = _context.ProductReviews
            .AsNoTracking()
            .Include(r => r.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            reviewsQuery = reviewsQuery.Where(r => r.CustomerName.Contains(s)
                || r.Comment.Contains(s)
                || (r.Product != null && r.Product.Name.Contains(s))
                || (r.CustomerEmail != null && r.CustomerEmail.Contains(s))
                || (r.CustomerPhone != null && r.CustomerPhone.Contains(s)));
        }

        if (reviewStatus == "pending")
        {
            reviewsQuery = reviewsQuery.Where(r => !r.IsApproved);
        }
        else if (reviewStatus == "approved")
        {
            reviewsQuery = reviewsQuery.Where(r => r.IsApproved);
        }

        var reviews = await reviewsQuery.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var settings = await _context.HomeSettings.FirstOrDefaultAsync() ?? new HomeSetting();

        ViewBag.TotalCount = await _context.ContactMessages.CountAsync();
        ViewBag.UnreadCount = await _context.ContactMessages.CountAsync(m => !m.IsRead);
        ViewBag.TotalReviewsCount = await _context.ProductReviews.CountAsync();
        ViewBag.PendingReviewsCount = await _context.ProductReviews.CountAsync(r => !r.IsApproved);
        ViewBag.ApprovedReviewsCount = await _context.ProductReviews.CountAsync(r => r.IsApproved);
        ViewBag.Reviews = reviews;
        ViewBag.Settings = settings;
        ViewBag.CurrentSearch = search;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentReviewStatus = reviewStatus ?? "all";
        ViewBag.ActiveTab = tab ?? (reviewStatus != null ? "reviews" : "messages");

        return View(messages);
    }

    // POST: /Messages/ToggleRead/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRead(int id)
    {
        var msg = await _context.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();

        msg.IsRead = !msg.IsRead;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = msg.IsRead ? "تم تحديد الرسالة كمقروءة." : "تم تحديد الرسالة كغير مقروءة.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Messages/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var msg = await _context.ContactMessages.FindAsync(id);
        if (msg != null)
        {
            _context.ContactMessages.Remove(msg);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الرسالة بنجاح.";
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: /Messages/UpdateSocialLinks
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSocialLinks(string facebookUrl, string instagramUrl, string whatsAppNumber, string? contactPhone, string? contactEmail, string? contactWorkingHours, string? atelierName, string? atelierAddress)
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
        }

        settings.FacebookUrl = facebookUrl?.Trim() ?? "";
        settings.InstagramUrl = instagramUrl?.Trim() ?? "";
        settings.WhatsAppNumber = whatsAppNumber?.Trim() ?? "";
        settings.ContactPhone = contactPhone?.Trim() ?? "";
        settings.ContactEmail = contactEmail?.Trim() ?? "";
        settings.ContactWorkingHours = !string.IsNullOrWhiteSpace(contactWorkingHours) ? contactWorkingHours.Trim() : "Daily: 10:00 AM – 11:00 PM CLT";
        settings.AtelierName = !string.IsNullOrWhiteSpace(atelierName) ? atelierName.Trim() : "Zamalek & New Cairo Salons";
        settings.AtelierAddress = !string.IsNullOrWhiteSpace(atelierAddress) ? atelierAddress.Trim() : "Cairo & Giza, Arab Republic of Egypt";

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم حفظ وتحديث لينكات السوشيال ميديا وأرقام التواصل بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Messages/ToggleApproveReview/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleApproveReview(int id)
    {
        var review = await _context.ProductReviews.Include(r => r.Product).FirstOrDefaultAsync(r => r.Id == id);
        if (review == null) return NotFound();

        review.IsApproved = !review.IsApproved;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = review.IsApproved 
            ? $"تم اعتماد تقييم ({review.CustomerName}) للمنتج ({review.Product?.Name}) بنجاح، ويظهر الآن في صفحة المنتج."
            : $"تم إلغاء اعتماد تقييم ({review.CustomerName}) وإخفاؤه من صفحة المنتج.";

        return RedirectToAction(nameof(Index), new { tab = "reviews" });
    }

    // POST: /Messages/DeleteReview/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _context.ProductReviews.FindAsync(id);
        if (review != null)
        {
            _context.ProductReviews.Remove(review);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف التقييم بنجاح.";
        }
        return RedirectToAction(nameof(Index), new { tab = "reviews" });
    }
}

