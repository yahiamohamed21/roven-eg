using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class CartController : Controller
{
    private const string CartSessionKey = "Stockify_Cart";
    private readonly ApplicationDbContext _context;
    private readonly IAntiforgery _antiforgery;

    public CartController(ApplicationDbContext context, IAntiforgery antiforgery)
    {
        _context = context;
        _antiforgery = antiforgery;
    }

    // GET: /Cart
    public IActionResult Index()
    {
        var cart = GetCartFromSession();
        return View(cart);
    }

    // GET: /Cart/GetCartSummary (used for header badge, quick cart, and syncing fresh anti-forgery tokens)
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetCartSummary()
    {
        var cart = GetCartFromSession();
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Json(new
        {
            count = cart.TotalItemCount,
            subtotal = cart.Subtotal,
            tax = cart.Tax,
            grandTotal = cart.GrandTotal,
            csrfToken = tokens.RequestToken
        });
    }

    // POST: /Cart/AddToCart
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1, int? sizeId = null, string? testerIds = null)
    {
        if (quantity < 1) quantity = 1;

        var product = await _context.Products
            .Include(p => p.ProductSizes)
            .Include(p => p.TesterOptions)
                .ThenInclude(t => t.FragranceProduct)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && p.IsPublished);

        if (product == null)
        {
            return Json(new { success = false, message = "Product is unavailable or does not exist." });
        }

        if (product.StockQuantity <= 0)
        {
            return Json(new { success = false, message = $"Sorry, '{product.Name}' is currently out of stock." });
        }

        var selectedTesterIds = new List<int>();
        string? selectedTestersSummary = null;

        if (product.IsTester)
        {
            var allowedLookup = product.TesterOptions
                .Where(o => o.FragranceProduct != null && o.FragranceProduct.IsActive)
                .ToDictionary(o => o.FragranceProductId, o => o.FragranceProduct!.Name);

            if (allowedLookup.Count > 0)
            {
                int configuredReq = product.TesterRequiredCount > 0 ? product.TesterRequiredCount : 4;
                int reqCount = Math.Min(configuredReq, allowedLookup.Count);

                var parsedIds = (testerIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var tid) ? tid : 0)
                    .Where(tid => tid > 0 && allowedLookup.ContainsKey(tid))
                    .Distinct()
                    .ToList();

                if (parsedIds.Count != reqCount)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"هذا المنتج تيستر ويتطلب اختيار {reqCount} عطور بالضبط من القائمة المتاحة."
                    });
                }

                selectedTestersSummary = string.Join("، ", parsedIds.Select(id => allowedLookup[id]));
                selectedTesterIds = parsedIds.OrderBy(id => id).ToList();
            }
        }

        var selectedSize = sizeId.HasValue && sizeId.Value > 0
            ? product.ProductSizes.FirstOrDefault(s => s.Id == sizeId.Value)
            : (product.ProductSizes.OrderByDescending(s => s.IsDefault).ThenBy(s => s.DisplayOrder).FirstOrDefault());

        var resolvedSizeId = selectedSize?.Id;
        var resolvedSizeLabel = selectedSize?.SizeLabel;

        var candidateKey = selectedTesterIds.Any()
            ? $"{productId}_{resolvedSizeId ?? 0}_{string.Join("-", selectedTesterIds)}"
            : $"{productId}_{resolvedSizeId ?? 0}";

        var cart = GetCartFromSession();
        var existingItem = cart.Items.FirstOrDefault(i => string.Equals(i.LineKey, candidateKey, StringComparison.Ordinal));
        var currentTotalProductQty = cart.Items.Where(i => i.ProductId == productId).Sum(i => i.Quantity);

        var targetTotalQty = currentTotalProductQty + quantity;
        if (targetTotalQty > product.StockQuantity)
        {
            return Json(new
            {
                success = false,
                message = $"Cannot add {quantity} more unit(s). Only {product.StockQuantity} unit(s) are available in stock."
            });
        }

        var targetQuantity = (existingItem?.Quantity ?? 0) + quantity;

        // Determine effective price: explicit non-default size uses exact size price; default size respects any active HomeOffer
        var baseSizePrice = selectedSize?.SellingPrice ?? product.Price;
        decimal effectivePrice = baseSizePrice;
        if (selectedSize == null || selectedSize.IsDefault)
        {
            var activeOffer = await _context.HomeOffers
                .FirstOrDefaultAsync(o => o.ProductId == productId && o.IsActive);
            if (activeOffer != null && activeOffer.OfferPrice > 0)
            {
                effectivePrice = activeOffer.OfferPrice;
            }
        }

        if (existingItem != null)
        {
            existingItem.Quantity = targetQuantity;
            existingItem.UnitPrice = effectivePrice;
            existingItem.SizeLabel = resolvedSizeLabel;
            existingItem.SelectedTesterProductIds = selectedTesterIds;
            existingItem.SelectedTestersSummary = selectedTestersSummary;
            existingItem.StockAvailable = product.StockQuantity;
        }
        else
        {
            cart.Items.Add(new CartItemViewModel
            {
                ProductId = product.Id,
                ProductSizeId = resolvedSizeId,
                SizeLabel = resolvedSizeLabel,
                SelectedTesterProductIds = selectedTesterIds,
                SelectedTestersSummary = selectedTestersSummary,
                ProductName = product.Name,
                Sku = product.Sku ?? "",
                UnitPrice = effectivePrice,
                Quantity = quantity,
                StockAvailable = product.StockQuantity,
                ImageUrl = product.ImageUrl
            });
        }

        SaveCartToSession(cart);

        var displayTitle = string.IsNullOrWhiteSpace(resolvedSizeLabel)
            ? product.Name
            : $"{product.Name} ({resolvedSizeLabel})";

        return Json(new
        {
            success = true,
            cartCount = cart.TotalItemCount,
            message = $"'{displayTitle}' added to your shopping cart."
        });
    }

    // POST: /Cart/AddOfferToCart (Supports Single and Duo 2-Perfume Offers)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOfferToCart(int offerId)
    {
        var offer = await _context.HomeOffers
            .Include(o => o.Product).ThenInclude(p => p!.ProductSizes)
            .Include(o => o.SecondProduct).ThenInclude(p => p!.ProductSizes)
            .Include(o => o.ThirdProduct).ThenInclude(p => p!.ProductSizes)
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

        var prod3 = (offer.ThirdProductId.HasValue && offer.ThirdProductId.Value > 0) ? offer.ThirdProduct : null;
        if (prod3 != null && prod3.StockQuantity <= 0)
        {
            return Json(new { success = false, message = $"عفواً، نفد مخزون العطر '{prod3.Name}'." });
        }

        var cart = GetCartFromSession();

        if (prod2 != null)
        {
            // Multi-Product Offer (Duo or Trio): Split offer price proportionally between all perfumes in the offer
            var prods = new List<Product> { prod1, prod2 };
            if (prod3 != null) prods.Add(prod3);

            var totalOrig = prods.Sum(p => p.Price);
            decimal allocatedSum = 0m;
            for (int pIdx = 0; pIdx < prods.Count; pIdx++)
            {
                var curProd = prods[pIdx];
                decimal curPrice;
                if (pIdx == prods.Count - 1)
                {
                    // Last item takes remainder to guarantee exact total sum matches offer.OfferPrice
                    curPrice = offer.OfferPrice - allocatedSum;
                }
                else
                {
                    if (totalOrig > 0)
                    {
                        var ratio = curProd.Price / totalOrig;
                        curPrice = Math.Round(offer.OfferPrice * ratio, 2);
                    }
                    else
                    {
                        curPrice = Math.Round(offer.OfferPrice / prods.Count, 2);
                    }
                    allocatedSum += curPrice;
                }

                var defSize = curProd.ProductSizes
                    .OrderByDescending(s => s.IsDefault)
                    .ThenBy(s => s.DisplayOrder)
                    .FirstOrDefault();

                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == curProd.Id && i.ProductSizeId == defSize?.Id);
                if (existingItem != null)
                {
                    existingItem.Quantity += 1;
                    existingItem.UnitPrice = curPrice;
                    existingItem.SizeLabel = defSize?.SizeLabel;
                    existingItem.StockAvailable = curProd.StockQuantity;
                }
                else
                {
                    cart.Items.Add(new CartItemViewModel
                    {
                        ProductId = curProd.Id,
                        ProductSizeId = defSize?.Id,
                        SizeLabel = defSize?.SizeLabel,
                        ProductName = curProd.Name,
                        Sku = curProd.Sku ?? "",
                        UnitPrice = curPrice,
                        Quantity = 1,
                        StockAvailable = curProd.StockQuantity,
                        ImageUrl = curProd.ImageUrl
                    });
                }
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
            var defSize1 = prod1.ProductSizes
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.DisplayOrder)
                .FirstOrDefault();
            var item1 = cart.Items.FirstOrDefault(i => i.ProductId == prod1.Id && i.ProductSizeId == defSize1?.Id);
            if (item1 != null)
            {
                item1.Quantity += 1;
                item1.UnitPrice = effectivePrice;
                item1.SizeLabel = defSize1?.SizeLabel;
                item1.StockAvailable = prod1.StockQuantity;
            }
            else
            {
                cart.Items.Add(new CartItemViewModel
                {
                    ProductId = prod1.Id,
                    ProductSizeId = defSize1?.Id,
                    SizeLabel = defSize1?.SizeLabel,
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
    public async Task<IActionResult> UpdateQuantity(int productId, int quantity, int? sizeId = null, string? lineKey = null)
    {
        var cart = GetCartFromSession();
        var item = !string.IsNullOrWhiteSpace(lineKey)
            ? cart.Items.FirstOrDefault(i => string.Equals(i.LineKey, lineKey, StringComparison.Ordinal))
            : cart.Items.FirstOrDefault(i => i.ProductId == productId && (!sizeId.HasValue || i.ProductSizeId == sizeId.Value));

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

        var product = await _context.Products
            .Include(p => p.ProductSizes)
            .FirstOrDefaultAsync(p => p.Id == productId);

        var otherSizeQty = cart.Items
            .Where(i => i.ProductId == productId && !ReferenceEquals(i, item))
            .Sum(i => i.Quantity);

        if (product != null && (otherSizeQty + quantity) > product.StockQuantity)
        {
            var remainingForThisLine = Math.Max(1, product.StockQuantity - otherSizeQty);
            return Json(new
            {
                success = false,
                message = $"Only {product.StockQuantity} unit(s) available in stock across selected sizes.",
                stockAvailable = remainingForThisLine
            });
        }

        item.Quantity = quantity;
        if (product != null)
        {
            var matchedSize = item.ProductSizeId.HasValue
                ? product.ProductSizes.FirstOrDefault(s => s.Id == item.ProductSizeId.Value)
                : product.ProductSizes.OrderByDescending(s => s.IsDefault).ThenBy(s => s.DisplayOrder).FirstOrDefault();

            var unitPrice = matchedSize?.SellingPrice ?? product.Price;
            if (matchedSize == null || matchedSize.IsDefault)
            {
                var activeOffer = await _context.HomeOffers
                    .FirstOrDefaultAsync(o => o.ProductId == productId && o.IsActive);
                if (activeOffer != null && activeOffer.OfferPrice > 0)
                {
                    unitPrice = activeOffer.OfferPrice;
                }
            }

            item.UnitPrice = unitPrice;
            if (matchedSize != null)
            {
                item.ProductSizeId = matchedSize.Id;
                item.SizeLabel = matchedSize.SizeLabel;
            }
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
    [ValidateAntiForgeryToken]
    public IActionResult RemoveFromCart(int productId, int? sizeId = null, string? lineKey = null)
    {
        var cart = GetCartFromSession();
        var item = !string.IsNullOrWhiteSpace(lineKey)
            ? cart.Items.FirstOrDefault(i => string.Equals(i.LineKey, lineKey, StringComparison.Ordinal))
            : cart.Items.FirstOrDefault(i => i.ProductId == productId && (!sizeId.HasValue || i.ProductSizeId == sizeId.Value));

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
    [ValidateAntiForgeryToken]
    public IActionResult ClearCart()
    {
        HttpContext.Session.Remove(CartSessionKey);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, totalCount = 0 });
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/ApplyCoupon
    [HttpPost]
    [ValidateAntiForgeryToken]
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
    [ValidateAntiForgeryToken]
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

