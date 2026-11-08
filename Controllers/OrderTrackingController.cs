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

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return View(model);
        }

        model.HasSearched = true;
        var cleanNumber = orderNumber.Trim();

        var query = _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Customer)
            .Where(o => o.OrderNumber.ToLower() == cleanNumber.ToLower());

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var cleanPhone = phone.Trim();
            query = query.Where(o => o.CustomerPhone == cleanPhone || (o.Customer != null && o.Customer.PhoneNumber == cleanPhone));
        }

        model.Order = await query.FirstOrDefaultAsync();

        return View(model);
    }
}

