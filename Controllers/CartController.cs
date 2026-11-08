using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Enums;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class CartController : Controller
{
    private const string CartSessionKey = "Stockify_Cart";
    private readonly ApplicationDbContext _context;

    public CartController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Cart
    public IActionResult Index()
    {
        var cart = GetCartFromSession();
        return View(cart);
    }

    // GET: /Cart/GetCartSummary (used for header badge and quick cart)
    [HttpGet]
    public IActionResult GetCartSummary()
    {
        var cart = GetCartFromSession();
        return Json(new
        {
            count = cart.TotalItemCount,
            subtotal = cart.Subtotal,
            tax = cart.Tax,
            grandTotal = cart.GrandTotal
        });
    }

    // POST: /Cart/AddToCart
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        if (quantity < 1) quantity = 1;

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && p.IsPublished);

        if (product == null)
        {
            return Json(new { success = false, message = "Product is unavailable or does not exist." });
        }

        if (product.StockQuantity <= 0)
        {
            return Json(new { success = false, message = $"Sorry, '{product.Name}' is currently out of stock." });
        }

        var cart = GetCartFromSession();
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        var targetQuantity = (existingItem?.Quantity ?? 0) + quantity;
        if (targetQuantity > product.StockQuantity)
        {
            return Json(new
            {
                success = false,
                message = $"Cannot add {quantity} more unit(s). Only {product.StockQuantity} unit(s) are available in stock."
            });
        }

        // Determine effective price: Check if product is part of an active HomeOffer
        var activeOffer = await _context.HomeOffers
            .FirstOrDefaultAsync(o => o.ProductId == productId && o.IsActive);
        var effectivePrice = (activeOffer != null && activeOffer.OfferPrice > 0) ? activeOffer.OfferPrice : product.Price;

        if (existingItem != null)
        {
            existingItem.Quantity = targetQuantity;
            existingItem.UnitPrice = effectivePrice; // keep refreshed with offer price
            existingItem.StockAvailable = product.StockQuantity;
        }
        else
        {
            cart.Items.Add(new CartItemViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku ?? "",
                UnitPrice = effectivePrice,
                Quantity = quantity,
                StockAvailable = product.StockQuantity,
                ImageUrl = product.ImageUrl
            });
        }

        SaveCartToSession(cart);

        return Json(new
        {
            success = true,
            cartCount = cart.TotalItemCount,
            message = $"'{product.Name}' added to your shopping cart."
        });
    }

    // POST: /Cart/AddOfferToCart (Supports Single and Duo 2-Perfume Offers)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOfferToCart(int offerId)
    {
        var offer = await _context.HomeOffers
            .Include(o => o.Product)
            .Include(o => o.SecondProduct)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.IsActive);

        if (offer == null || offer.Product == null || !offer.Product.IsActive || !offer.Product.IsPublished)
        {
            return Json(new { success = false, message = "العرض المختار غير متوفر حالياً." });
        }

        var prod1 = offer.Product;
        if (prod1.StockQuantity <= 0)
        {
            return Json(new { success = false, message = $"عفواً، نفد مخزون العطر '{prod1.Name}'." });
        }

        var prod2 = (offer.SecondProductId.HasValue && offer.SecondProductId.Value > 0) ? offer.SecondProduct : null;
        if (prod2 != null && prod2.StockQuantity <= 0)
        {
            return Json(new { success = false, message = $"عفواً، نفد مخزون العطر '{prod2.Name}'." });
        }

        var cart = GetCartFromSession();

        if (prod2 != null)
        {
            // Duo Offer: Split offer price proportionally between both perfumes
            var totalOrig = prod1.Price + prod2.Price;
            decimal price1;
            decimal price2;
            if (totalOrig > 0)
            {
                var ratio1 = prod1.Price / totalOrig;
                price1 = Math.Round(offer.OfferPrice * ratio1, 2);
                price2 = offer.OfferPrice - price1;
            }
            else
            {
                price1 = Math.Round(offer.OfferPrice / 2, 2);
                price2 = offer.OfferPrice - price1;
            }

            // Add Product 1
            var item1 = cart.Items.FirstOrDefault(i => i.ProductId == prod1.Id);
            if (item1 != null)
            {
                item1.Quantity += 1;
                item1.UnitPrice = price1;
                item1.StockAvailable = prod1.StockQuantity;
            }
            else
            {
                cart.Items.Add(new CartItemViewModel
                {
                    ProductId = prod1.Id,
                    ProductName = prod1.Name,
                    Sku = prod1.Sku ?? "",
                    UnitPrice = price1,
                    Quantity = 1,
                    StockAvailable = prod1.StockQuantity,
                    ImageUrl = prod1.ImageUrl
                });
            }

            // Add Product 2
            var item2 = cart.Items.FirstOrDefault(i => i.ProductId == prod2.Id);
            if (item2 != null)
            {
                item2.Quantity += 1;
                item2.UnitPrice = price2;
                item2.StockAvailable = prod2.StockQuantity;
            }
            else
            {
                cart.Items.Add(new CartItemViewModel
                {
                    ProductId = prod2.Id,
                    ProductName = prod2.Name,
                    Sku = prod2.Sku ?? "",
                    UnitPrice = price2,
                    Quantity = 1,
                    StockAvailable = prod2.StockQuantity,
                    ImageUrl = prod2.ImageUrl
                });
            }

            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                cartCount = cart.TotalItemCount,
                message = $"تمت إضافة باقة العطرين ({prod1.Name} + {prod2.Name}) إلى حقيبة التسوق بنجاح!"
            });
        }
        else
        {
            // Single Product Offer
            var effectivePrice = offer.OfferPrice > 0 ? offer.OfferPrice : prod1.Price;
            var item1 = cart.Items.FirstOrDefault(i => i.ProductId == prod1.Id);
            if (item1 != null)
            {
                item1.Quantity += 1;
                item1.UnitPrice = effectivePrice;
                item1.StockAvailable = prod1.StockQuantity;
            }
            else
            {
                cart.Items.Add(new CartItemViewModel
                {
                    ProductId = prod1.Id,
                    ProductName = prod1.Name,
                    Sku = prod1.Sku ?? "",
                    UnitPrice = effectivePrice,
                    Quantity = 1,
                    StockAvailable = prod1.StockQuantity,
                    ImageUrl = prod1.ImageUrl
                });
            }

            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                cartCount = cart.TotalItemCount,
                message = $"تمت إضافة '{prod1.Name}' بسعر العرض إلى حقيبة التسوق بنجاح!"
            });
        }
    }

    // POST: /Cart/UpdateQuantity
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
    {
        var cart = GetCartFromSession();
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
        {
            return Json(new { success = false, message = "Item not found in cart." });
        }

        if (quantity <= 0)
        {
            cart.Items.Remove(item);
            SaveCartToSession(cart);
            return Json(new
            {
                success = true,
                removed = true,
                totalCount = cart.TotalItemCount,
                subtotal = cart.Subtotal,
                tax = cart.Tax,
                grandTotal = cart.GrandTotal
            });
        }

        var product = await _context.Products.FindAsync(productId);
        if (product != null && quantity > product.StockQuantity)
        {
            return Json(new
            {
                success = false,
                message = $"Only {product.StockQuantity} unit(s) available in stock.",
                stockAvailable = product.StockQuantity
            });
        }

        item.Quantity = quantity;
        if (product != null)
        {
            item.UnitPrice = product.Price;
            item.StockAvailable = product.StockQuantity;
        }

        SaveCartToSession(cart);

        return Json(new
        {
            success = true,
            removed = false,
            itemTotal = item.LineTotal,
            totalCount = cart.TotalItemCount,
            subtotal = cart.Subtotal,
            tax = cart.Tax,
            grandTotal = cart.GrandTotal
        });
    }

    // POST: /Cart/RemoveFromCart
    [HttpPost]
    public IActionResult RemoveFromCart(int productId)
    {
        var cart = GetCartFromSession();
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item != null)
        {
            cart.Items.Remove(item);
            SaveCartToSession(cart);
        }

        return Json(new
        {
            success = true,
            totalCount = cart.TotalItemCount,
            subtotal = cart.Subtotal,
            tax = cart.Tax,
            grandTotal = cart.GrandTotal
        });
    }

    // POST: /Cart/ClearCart
    [HttpPost]
    public IActionResult ClearCart()
    {
        HttpContext.Session.Remove(CartSessionKey);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/ApplyCoupon
    [HttpPost]
    public async Task<IActionResult> ApplyCoupon(string couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return Json(new { success = false, message = "يرجى كتابة كود الكوبون." });
        }

        var cart = GetCartFromSession();
        if (cart.Items.Count == 0)
        {
            return Json(new { success = false, message = "حقيبة التسوق فارغة." });
        }

        var clean = couponCode.Trim().ToUpper();
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == clean);

        if (coupon == null || !coupon.IsActive)
        {
            return Json(new { success = false, message = "كود الكوبون غير صحيح أو غير مفعل." });
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
            return Json(new { success = false, message = "تم استنفاد الحد الأقصى لاستخدام هذا الكوبون." });
        }

        if (coupon.MinimumOrderAmount.HasValue && cart.Subtotal < coupon.MinimumOrderAmount.Value)
        {
            return Json(new { success = false, message = $"الحد الأدنى للطلب لتطبيق الكوبون هو {coupon.MinimumOrderAmount.Value:N2} ج.م." });
        }

        decimal discount = 0;
        if (coupon.DiscountType == CouponDiscountType.Percentage)
        {
            discount = Math.Round((cart.Subtotal * coupon.DiscountValue) / 100m, 2);
            if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
            {
                discount = coupon.MaxDiscountAmount.Value;
            }
        }
        else
        {
            discount = Math.Min(cart.Subtotal, coupon.DiscountValue);
        }

        cart.AppliedCouponCode = coupon.Code;
        cart.DiscountAmount = discount;
        SaveCartToSession(cart);

        return Json(new
        {
            success = true,
            code = coupon.Code,
            discountAmount = discount,
            subtotal = cart.Subtotal,
            grandTotal = cart.GrandTotal,
            message = $"تم تطبيق خصم الكوبون '{coupon.Code}' بقيمة {discount:N2} ج.م بنجاح!"
        });
    }

    // POST: /Cart/RemoveCoupon
    [HttpPost]
    public IActionResult RemoveCoupon()
    {
        var cart = GetCartFromSession();
        cart.AppliedCouponCode = null;
        cart.DiscountAmount = 0m;
        SaveCartToSession(cart);

        return Json(new
        {
            success = true,
            subtotal = cart.Subtotal,
            grandTotal = cart.GrandTotal,
            message = "تم إلغاء الكوبون."
        });
    }

    // Session Helpers
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

    private void SaveCartToSession(CartViewModel cart)
    {
        var json = JsonSerializer.Serialize(cart);
        HttpContext.Session.SetString(CartSessionKey, json);
    }
}

