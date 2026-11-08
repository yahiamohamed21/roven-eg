using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class CheckoutController : Controller
{
    private const string CartSessionKey = "Stockify_Cart";
    private readonly ApplicationDbContext _context;
    private readonly IOrderService _orderService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ApplicationDbContext context,
        IOrderService orderService,
        ILogger<CheckoutController> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }

    // GET: /Checkout
    public async Task<IActionResult> Index()
    {
        var cart = GetCartFromSession();
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Store");
        }

        var govs = await _context.Governorates
            .Where(g => g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.NameAr)
            .ToListAsync();

        var defaultGov = govs.FirstOrDefault(g => g.NameAr == "القاهرة" || g.NameEn.Equals("Cairo", StringComparison.OrdinalIgnoreCase)) ?? govs.FirstOrDefault();
        var defaultFee = defaultGov?.ShippingFee ?? 50m;
        cart.ShippingFee = defaultFee;

        var model = new CheckoutViewModel
        {
            Cart = cart,
            CouponCode = cart.AppliedCouponCode,
            DiscountAmount = cart.DiscountAmount,
            City = defaultGov?.NameAr ?? "القاهرة",
            ShippingFee = defaultFee,
            PaymentMethod = "Cash on Delivery",
            AvailableGovernorates = govs
        };

        return View(model);
    }

    // POST: /Checkout/PlaceOrder
    // Atomic concurrency-safe order placement delegating to unified IOrderService
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
    {
        var cart = GetCartFromSession();
        if (cart.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "حقيبة التسوق فارغة. يرجى إضافة عطور قبل إتمام الطلب.");
            model.Cart = cart;
            model.AvailableGovernorates = await _context.Governorates.Where(g => g.IsActive).OrderBy(g => g.DisplayOrder).ThenBy(g => g.NameAr).ToListAsync();
            return View("Index", model);
        }

        // Resolve exact server-verified shipping rate for the chosen governorate
        var selectedCity = model.City?.Trim() ?? "";
        var matchedGov = await _context.Governorates
            .FirstOrDefaultAsync(g => g.IsActive && (g.NameAr == selectedCity || g.NameEn == selectedCity));

        var shippingFee = matchedGov?.ShippingFee ?? 50.00m;
        cart.ShippingFee = shippingFee;
        model.ShippingFee = shippingFee;

        if (!ModelState.IsValid)
        {
            model.Cart = cart;
            model.AvailableGovernorates = await _context.Governorates.Where(g => g.IsActive).OrderBy(g => g.DisplayOrder).ThenBy(g => g.NameAr).ToListAsync();
            return View("Index", model);
        }

        try
        {
            // Build unified request for OrderService
            var orderRequest = new CreateOrderRequest
            {
                OrderSource = OrderSource.Website,
                Status = OrderStatus.Pending,
                CustomerName = model.CustomerName?.Trim(),
                CustomerPhone = model.CustomerPhone?.Trim(),
                CustomerEmail = model.CustomerEmail?.Trim(),
                ShippingAddress = model.ShippingAddress?.Trim(),
                City = matchedGov?.NameAr ?? selectedCity,
                ShippingFee = shippingFee,
                OrderNotes = model.OrderNotes?.Trim(),
                CouponCode = model.CouponCode ?? cart.AppliedCouponCode,
                PaymentMethod = model.PaymentMethod ?? "Cash on Delivery",
                Items = cart.Items.Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            // Unified atomic stock deduction & order creation
            var result = await _orderService.CreateOrderAsync(orderRequest);

            if (!result.Success)
            {
                _logger.LogWarning("Checkout conflict/rejection: {ErrorMessage}", result.ErrorMessage);
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر إتمام الطلب نظراً لتغير كمية المخزون المتاحة حالياً.");
                model.Cart = cart;
                model.AvailableGovernorates = await _context.Governorates.Where(g => g.IsActive).OrderBy(g => g.DisplayOrder).ThenBy(g => g.NameAr).ToListAsync();
                return View("Index", model);
            }

            // Mark any matching abandoned checkout draft as recovered!
            try
            {
                var cleanPhone = model.CustomerPhone?.Trim().Replace(" ", "").Replace("-", "");
                var sessionId = HttpContext.Session.Id;
                var abandonedRecord = await _context.AbandonedCheckouts
                    .Where(a => !a.IsRecovered && ((!string.IsNullOrEmpty(cleanPhone) && a.CustomerPhone == cleanPhone) || (!string.IsNullOrEmpty(sessionId) && a.SessionId == sessionId)))
                    .OrderByDescending(a => a.LastUpdatedAt)
                    .FirstOrDefaultAsync();

                if (abandonedRecord != null)
                {
                    abandonedRecord.IsRecovered = true;
                    abandonedRecord.RecoveredOrderId = result.Order?.Id;
                    abandonedRecord.LastUpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark abandoned checkout as recovered for order {OrderNumber}", result.OrderNumber);
            }

            // Clear session shopping bag upon successful atomic order commitment
            HttpContext.Session.Remove(CartSessionKey);

            return RedirectToAction(nameof(Confirmation), new { orderNumber = result.OrderNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing online store order.");
            ModelState.AddModelError(string.Empty, "حدث خطأ غير متوقع أثناء معالجة الطلب. يرجى المحاولة مرة أخرى.");
            model.Cart = cart;
            return View("Index", model);
        }
    }

    // POST: /Checkout/CaptureAbandonedCheckout
    // Real-time asynchronous capture of customer checkout info before order finalization
    [HttpPost]
    public async Task<IActionResult> CaptureAbandonedCheckout([FromBody] AbandonedCheckoutDraftDto draft)
    {
        if (draft == null || string.IsNullOrWhiteSpace(draft.CustomerPhone))
        {
            return Json(new { success = false, message = "Phone number is required." });
        }

        var cleanPhone = draft.CustomerPhone.Trim().Replace(" ", "").Replace("-", "");
        if (cleanPhone.Length < 7)
        {
            return Json(new { success = false, message = "Phone number is incomplete." });
        }

        var cart = GetCartFromSession();
        if (cart.Items.Count == 0)
        {
            return Json(new { success = false, message = "Cart is empty." });
        }

        var sessionId = HttpContext.Session.Id;
        var now = DateTime.UtcNow;

        // Check if an active unrecovered abandoned record exists for this session or phone
        var existingDraft = await _context.AbandonedCheckouts
            .Where(a => !a.IsRecovered && ((!string.IsNullOrEmpty(sessionId) && a.SessionId == sessionId) || a.CustomerPhone == cleanPhone))
            .OrderByDescending(a => a.LastUpdatedAt)
            .FirstOrDefaultAsync();

        // Serialize cart items into JSON
        var itemsSummary = cart.Items.Select(i => new
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.LineTotal,
            ImageUrl = i.ImageUrl
        }).ToList();

        var cartItemsJson = JsonSerializer.Serialize(itemsSummary);

        if (existingDraft != null)
        {
            if (!string.IsNullOrWhiteSpace(draft.CustomerName)) existingDraft.CustomerName = draft.CustomerName.Trim();
            existingDraft.CustomerPhone = cleanPhone;
            if (!string.IsNullOrWhiteSpace(draft.CustomerEmail)) existingDraft.CustomerEmail = draft.CustomerEmail.Trim();
            if (!string.IsNullOrWhiteSpace(draft.ShippingAddress)) existingDraft.ShippingAddress = draft.ShippingAddress.Trim();
            if (!string.IsNullOrWhiteSpace(draft.City)) existingDraft.City = draft.City.Trim();
            if (!string.IsNullOrWhiteSpace(draft.OrderNotes)) existingDraft.OrderNotes = draft.OrderNotes.Trim();
            existingDraft.CartItemsJson = cartItemsJson;
            existingDraft.TotalAmount = cart.GrandTotal;
            existingDraft.ItemCount = cart.TotalItemCount;
            existingDraft.LastUpdatedAt = now;
        }
        else
        {
            var newDraft = new AbandonedCheckout
            {
                SessionId = sessionId,
                CustomerName = !string.IsNullOrWhiteSpace(draft.CustomerName) ? draft.CustomerName.Trim() : "زائر بالمتجر (Guest)",
                CustomerPhone = cleanPhone,
                CustomerEmail = draft.CustomerEmail?.Trim(),
                ShippingAddress = draft.ShippingAddress?.Trim(),
                City = draft.City?.Trim(),
                OrderNotes = draft.OrderNotes?.Trim(),
                CartItemsJson = cartItemsJson,
                TotalAmount = cart.GrandTotal,
                ItemCount = cart.TotalItemCount,
                CreatedAt = now,
                LastUpdatedAt = now,
                IsRecovered = false
            };
            _context.AbandonedCheckouts.Add(newDraft);
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // GET: /Checkout/Confirmation?orderNumber=ORD-20261105-1234
    public async Task<IActionResult> Confirmation(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return RedirectToAction("Index", "Home");
        }

        var order = await _orderService.GetOrderByNumberAsync(orderNumber);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    private CartViewModel GetCartFromSession()
    {
        var json = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CartViewModel();
        }

        try
        {
            return JsonSerializer.Deserialize<CartViewModel>(json) ?? new CartViewModel();
        }
        catch
        {
            return new CartViewModel();
        }
    }
}
