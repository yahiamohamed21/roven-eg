using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class OrderTrackingController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrderTrackingController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /OrderTracking or /OrderTracking?orderNumber=ORD-20261105-1234
    public async Task<IActionResult> Index(string? orderNumber, string? phone)
    {
        var model = new OrderTrackingViewModel
        {
            QueryOrderNumber = orderNumber?.Trim(),
            QueryPhone = phone?.Trim(),
            HasSearched = false
        };

        if (string.IsNullOrWhiteSpace(orderNumber) && string.IsNullOrWhiteSpace(phone))
        {
            return View(model);
        }

        var siteSetting = await _context.HomeSettings.AsNoTracking().FirstOrDefaultAsync();
        model.WhatsAppNumber = siteSetting?.WhatsAppNumber ?? string.Empty;

        model.HasSearched = true;
        var cleanNumber = orderNumber?.Trim();
        var cleanPhone = string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim().Replace(" ", "").Replace("-", "");

        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(cleanNumber))
        {
            query = query.Where(o => o.OrderNumber.ToLower() == cleanNumber.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(cleanPhone))
        {
            query = query.Where(o =>
                o.CustomerPhone == cleanPhone ||
                (o.Customer != null && o.Customer.PhoneNumber == cleanPhone));
        }

        model.Order = await query
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefaultAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_TrackingResultPartial", model);
        }

        return View(model);
    }
}

