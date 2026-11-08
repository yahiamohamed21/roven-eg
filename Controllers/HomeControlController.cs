using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class HomeControlController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly Stockify.Services.Interfaces.IImageStorageService _imageStorageService;

    public HomeControlController(
        ApplicationDbContext context, 
        IWebHostEnvironment environment,
        Stockify.Services.Interfaces.IImageStorageService imageStorageService)
    {
        _context = context;
        _environment = environment;
        _imageStorageService = imageStorageService;
    }

    // GET: /HomeControl
    public async Task<IActionResult> Index()
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        ViewBag.HeroBanners = await _context.HeroBanners
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Id)
            .ToListAsync();

        ViewBag.Products = await _context.Products
            .Where(p => p.IsActive && p.IsPublished)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.HomeOffers = await _context.HomeOffers
            .Include(o => o.Product)
            .Include(o => o.SecondProduct)
            .OrderBy(o => o.DisplayOrder)
            .ThenByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(settings);
    }

    #region Hero Banners Management (Full-Image Hero Slides)

    private async Task<string?> SaveBannerFileAsync(IFormFile? file, string prefix)
    {
        if (file == null || file.Length == 0) return null;

        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".svg"];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) return null;

        var customFileName = $"{prefix}_{Guid.NewGuid():N}{ext}";
        return await _imageStorageService.UploadImageAsync(file, "roven/banners", customFileName);
    }

    // POST: /HomeControl/AddHeroBanner
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHeroBanner(
        IFormFile? desktopImage,
        string? desktopImageUrl,
        IFormFile? mobileImage,
        string? mobileImageUrl,
        string? title,
        string? redirectLink,
        string? altText,
        bool isActive = true)
    {
        string? finalDesktopUrl = await SaveBannerFileAsync(desktopImage, "desktop");
        if (string.IsNullOrWhiteSpace(finalDesktopUrl))
        {
            finalDesktopUrl = desktopImageUrl?.Trim();
        }

        if (string.IsNullOrWhiteSpace(finalDesktopUrl))
        {
            TempData["ErrorMessage"] = "يرجى رفع صورة الديسكتوب للبانر (Desktop Image) أو إدخال مسارها.";
            return RedirectToAction(nameof(Index));
        }

        string? finalMobileUrl = await SaveBannerFileAsync(mobileImage, "mobile");
        if (string.IsNullOrWhiteSpace(finalMobileUrl))
        {
            finalMobileUrl = mobileImageUrl?.Trim();
        }

        var bannerCount = await _context.HeroBanners.CountAsync();

        var banner = new HeroBanner
        {
            Title = string.IsNullOrWhiteSpace(title) ? $"Hero Banner Slide #{bannerCount + 1}" : title.Trim(),
            DesktopImageUrl = finalDesktopUrl,
            MobileImageUrl = finalMobileUrl,
            RedirectLink = string.IsNullOrWhiteSpace(redirectLink) ? "/Store" : redirectLink.Trim(),
            AltText = altText?.Trim(),
            IsActive = isActive,
            DisplayOrder = bannerCount + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.HeroBanners.Add(banner);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تمت إضافة البانر '{banner.Title}' بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/UpdateHeroBanner
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHeroBanner(
        int id,
        IFormFile? desktopImage,
        string? desktopImageUrl,
        IFormFile? mobileImage,
        string? mobileImageUrl,
        string? title,
        string? redirectLink,
        string? altText,
        bool isActive,
        int displayOrder)
    {
        var banner = await _context.HeroBanners.FindAsync(id);
        if (banner == null)
        {
            TempData["ErrorMessage"] = "البانر المطلوب غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var newDesktop = await SaveBannerFileAsync(desktopImage, "desktop");
        if (!string.IsNullOrWhiteSpace(newDesktop))
        {
            banner.DesktopImageUrl = newDesktop;
        }
        else if (!string.IsNullOrWhiteSpace(desktopImageUrl))
        {
            banner.DesktopImageUrl = desktopImageUrl.Trim();
        }

        var newMobile = await SaveBannerFileAsync(mobileImage, "mobile");
        if (!string.IsNullOrWhiteSpace(newMobile))
        {
            banner.MobileImageUrl = newMobile;
        }
        else if (!string.IsNullOrWhiteSpace(mobileImageUrl))
        {
            banner.MobileImageUrl = mobileImageUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            banner.Title = title.Trim();
        }
        banner.RedirectLink = string.IsNullOrWhiteSpace(redirectLink) ? "/Store" : redirectLink.Trim();
        banner.AltText = altText?.Trim();
        banner.IsActive = isActive;
        banner.DisplayOrder = displayOrder;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم تحديث بيانات البانر '{banner.Title}' بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/ToggleHeroBanner
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHeroBanner(int id)
    {
        var banner = await _context.HeroBanners.FindAsync(id);
        if (banner == null)
        {
            TempData["ErrorMessage"] = "البانر غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        banner.IsActive = !banner.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم {(banner.IsActive ? "تفعيل" : "إيقاف")} ظهور البانر '{banner.Title}' في المتجر.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/MoveHeroBanner
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveHeroBanner(int id, string direction)
    {
        var banners = await _context.HeroBanners.OrderBy(b => b.DisplayOrder).ThenBy(b => b.Id).ToListAsync();
        var currentIndex = banners.FindIndex(b => b.Id == id);
        if (currentIndex < 0)
        {
            return RedirectToAction(nameof(Index));
        }

        if (direction == "up" && currentIndex > 0)
        {
            var prev = banners[currentIndex - 1];
            var current = banners[currentIndex];
            var tempOrder = current.DisplayOrder;
            current.DisplayOrder = prev.DisplayOrder;
            prev.DisplayOrder = tempOrder;

            if (current.DisplayOrder == prev.DisplayOrder)
            {
                current.DisplayOrder = currentIndex;
                prev.DisplayOrder = currentIndex + 1;
            }
            await _context.SaveChangesAsync();
        }
        else if (direction == "down" && currentIndex < banners.Count - 1)
        {
            var next = banners[currentIndex + 1];
            var current = banners[currentIndex];
            var tempOrder = current.DisplayOrder;
            current.DisplayOrder = next.DisplayOrder;
            next.DisplayOrder = tempOrder;

            if (current.DisplayOrder == next.DisplayOrder)
            {
                current.DisplayOrder = currentIndex + 2;
                next.DisplayOrder = currentIndex + 1;
            }
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/DeleteHeroBanner
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHeroBanner(int id)
    {
        var banner = await _context.HeroBanners.FindAsync(id);
        if (banner == null)
        {
            TempData["ErrorMessage"] = "البانر غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var title = banner.Title;
        _context.HeroBanners.Remove(banner);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم حذف البانر '{title}' بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    #endregion

    // POST: /HomeControl/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(HomeSetting model)
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
        }

        settings.AnnouncementText = model.AnnouncementText?.Trim() ?? "";
        settings.AnnouncementActive = model.AnnouncementActive;

        settings.HeroTitle = model.HeroTitle?.Trim() ?? "";
        settings.HeroSubtitle = model.HeroSubtitle?.Trim() ?? "";
        settings.HeroBadgeText = model.HeroBadgeText?.Trim() ?? "";

        settings.Banner2Title = model.Banner2Title?.Trim() ?? "";
        settings.Banner2Subtitle = model.Banner2Subtitle?.Trim() ?? "";
        settings.Banner2BadgeText = model.Banner2BadgeText?.Trim() ?? "";
        settings.Banner2ButtonText = model.Banner2ButtonText?.Trim() ?? "";
        settings.Banner2ButtonLink = model.Banner2ButtonLink?.Trim() ?? "";
        settings.Banner2Active = model.Banner2Active;

        settings.Banner3Title = model.Banner3Title?.Trim() ?? "";
        settings.Banner3Subtitle = model.Banner3Subtitle?.Trim() ?? "";
        settings.Banner3BadgeText = model.Banner3BadgeText?.Trim() ?? "";
        settings.Banner3ButtonText = model.Banner3ButtonText?.Trim() ?? "";
        settings.Banner3ButtonLink = model.Banner3ButtonLink?.Trim() ?? "";
        settings.Banner3Active = model.Banner3Active;

        settings.SpotlightProductId = model.SpotlightProductId;
        settings.SpotlightPrice = model.SpotlightPrice;
        settings.SpotlightEndTime = model.SpotlightEndTime;
        settings.SpotlightAllocatedPercent = model.SpotlightAllocatedPercent;
        settings.SpotlightRemainingCount = model.SpotlightRemainingCount;
        settings.SpotlightActive = model.SpotlightActive;

        settings.FacebookUrl = model.FacebookUrl?.Trim() ?? "";
        settings.InstagramUrl = model.InstagramUrl?.Trim() ?? "";
        settings.WhatsAppNumber = model.WhatsAppNumber?.Trim() ?? "";
        settings.ContactPhone = model.ContactPhone?.Trim() ?? "";
        settings.ContactEmail = model.ContactEmail?.Trim() ?? "";
        settings.ContactWorkingHours = !string.IsNullOrWhiteSpace(model.ContactWorkingHours) ? model.ContactWorkingHours.Trim() : "Daily: 10:00 AM – 11:00 PM CLT";
        settings.AtelierName = !string.IsNullOrWhiteSpace(model.AtelierName) ? model.AtelierName.Trim() : "Zamalek & New Cairo Salons";
        settings.AtelierAddress = !string.IsNullOrWhiteSpace(model.AtelierAddress) ? model.AtelierAddress.Trim() : "Cairo & Giza, Arab Republic of Egypt";

        settings.ShowTrustBadges = model.ShowTrustBadges;
        settings.ShowCategoriesShowcase = model.ShowCategoriesShowcase;
        settings.ShowFeaturedSection = model.ShowFeaturedSection;
        settings.ShowBestSellersSection = model.ShowBestSellersSection;
        settings.ShowFaqSection = model.ShowFaqSection;
        settings.ShowNewsletterSection = model.ShowNewsletterSection;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم تحديث وتثبيت إعدادات الواجهة الرئيسية بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/AddHomeOffer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHomeOffer(int productId, int? secondProductId, string? title, decimal offerPrice, string? badgeText, int? allocatedPercent, int? remainingCount, DateTime? endTime)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            TempData["ErrorMessage"] = "العطر الأساسي المختار غير موجود في الكتالوج.";
            return RedirectToAction(nameof(Index));
        }

        Product? secondProduct = null;
        if (secondProductId.HasValue && secondProductId.Value > 0 && secondProductId.Value != productId)
        {
            secondProduct = await _context.Products.FindAsync(secondProductId.Value);
        }

        var totalOriginalPrice = product.Price + (secondProduct?.Price ?? 0m);
        if (offerPrice <= 0)
        {
            offerPrice = totalOriginalPrice;
        }

        var existingCount = await _context.HomeOffers.CountAsync();
        var defaultBadge = secondProduct != null ? "DUO BUNDLE • عرض عطرين" : "EXCLUSIVE • عرض خاص";

        var newOffer = new HomeOffer
        {
            ProductId = productId,
            SecondProductId = secondProduct?.Id,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            OfferPrice = offerPrice,
            BadgeText = string.IsNullOrWhiteSpace(badgeText) ? defaultBadge : badgeText.Trim(),
            AllocatedPercent = allocatedPercent.HasValue ? Math.Clamp(allocatedPercent.Value, 0, 100) : 82,
            RemainingCount = remainingCount.HasValue && remainingCount.Value > 0 ? remainingCount.Value : Math.Max(1, product.StockQuantity),
            EndTime = endTime ?? DateTime.UtcNow.AddDays(7),
            DisplayOrder = existingCount + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.HomeOffers.Add(newOffer);
        await _context.SaveChangesAsync();

        var successTitle = secondProduct != null ? $"عرض العطرين ({product.Name} + {secondProduct.Name})" : $"العطر '{product.Name}'";
        TempData["SuccessMessage"] = $"تمت إضافة {successTitle} بنجاح إلى قسم العروض بسعر {offerPrice:N0} ج.م!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/UpdateHomeOffer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHomeOffer(int id, int productId, int? secondProductId, string? title, decimal offerPrice, string? badgeText, int allocatedPercent, int remainingCount, DateTime? endTime, int displayOrder)
    {
        var offer = await _context.HomeOffers
            .Include(o => o.Product)
            .Include(o => o.SecondProduct)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (offer == null)
        {
            TempData["ErrorMessage"] = "العرض المطلوب غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        offer.ProductId = productId;
        if (secondProductId.HasValue && secondProductId.Value > 0 && secondProductId.Value != productId)
        {
            offer.SecondProductId = secondProductId.Value;
        }
        else
        {
            offer.SecondProductId = null;
        }

        offer.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        if (offerPrice > 0)
        {
            offer.OfferPrice = offerPrice;
        }

        var defaultBadge = offer.SecondProductId.HasValue ? "DUO BUNDLE • عرض عطرين" : "OFFER • عرض خاص";
        offer.BadgeText = string.IsNullOrWhiteSpace(badgeText) ? defaultBadge : badgeText.Trim();
        offer.AllocatedPercent = Math.Clamp(allocatedPercent, 0, 100);
        offer.RemainingCount = Math.Max(0, remainingCount);
        offer.EndTime = endTime;
        offer.DisplayOrder = displayOrder;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم تحديث بيانات العرض بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/ToggleHomeOffer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeOffer(int id)
    {
        var offer = await _context.HomeOffers.Include(o => o.Product).FirstOrDefaultAsync(o => o.Id == id);
        if (offer == null)
        {
            TempData["ErrorMessage"] = "العرض غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        offer.IsActive = !offer.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم {(offer.IsActive ? "تفعيل" : "إيقاف")} عرض '{offer.Product?.Name ?? "المنتج"}' في قسم العروض.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/DeleteHomeOffer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHomeOffer(int id)
    {
        var offer = await _context.HomeOffers.Include(o => o.Product).FirstOrDefaultAsync(o => o.Id == id);
        if (offer == null)
        {
            TempData["ErrorMessage"] = "العرض غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var prodName = offer.Product?.Name ?? "المنتج";
        _context.HomeOffers.Remove(offer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم حذف '{prodName}' من قسم العروض بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/SyncImagesToCloudinary
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncImagesToCloudinary()
    {
        if (!_imageStorageService.IsConfigured)
        {
            return Json(new 
            { 
                success = false, 
                message = "بيانات حساب Cloudinary غير مكتملة. يرجى إدخال CloudName و ApiKey و ApiSecret في appsettings.json أولاً." 
            });
        }

        int syncedCount = 0;
        long bytesSaved = 0;

        // 1. Sync Products Images
        var products = await _context.Products.ToListAsync();
        foreach (var p in products)
        {
            if (!string.IsNullOrWhiteSpace(p.ImageUrl) && !p.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = p.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);

                if (System.IO.File.Exists(fullLocalPath))
                {
                    var fileInfo = new FileInfo(fullLocalPath);
                    bytesSaved += fileInfo.Length;

                    await using (var stream = System.IO.File.OpenRead(fullLocalPath))
                    {
                        var newUrl = await _imageStorageService.UploadFileAsync(stream, fileInfo.Name, "roven/products");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            p.ImageUrl = newUrl;
                            syncedCount++;
                        }
                    }
                }
            }
        }

        // 2. Sync Hero Banners Images
        var banners = await _context.HeroBanners.ToListAsync();
        foreach (var b in banners)
        {
            if (!string.IsNullOrWhiteSpace(b.DesktopImageUrl) && !b.DesktopImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = b.DesktopImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                if (System.IO.File.Exists(fullLocalPath))
                {
                    var fileInfo = new FileInfo(fullLocalPath);
                    bytesSaved += fileInfo.Length;

                    await using (var stream = System.IO.File.OpenRead(fullLocalPath))
                    {
                        var newUrl = await _imageStorageService.UploadFileAsync(stream, fileInfo.Name, "roven/banners");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            b.DesktopImageUrl = newUrl;
                            syncedCount++;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(b.MobileImageUrl) && !b.MobileImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPath = b.MobileImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                if (System.IO.File.Exists(fullLocalPath))
                {
                    var fileInfo = new FileInfo(fullLocalPath);
                    bytesSaved += fileInfo.Length;

                    await using (var stream = System.IO.File.OpenRead(fullLocalPath))
                    {
                        var newUrl = await _imageStorageService.UploadFileAsync(stream, fileInfo.Name, "roven/banners");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            b.MobileImageUrl = newUrl;
                            syncedCount++;
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        var mbSaved = Math.Round((double)bytesSaved / (1024 * 1024), 2);
        return Json(new
        {
            success = true,
            syncedCount = syncedCount,
            mbSaved = mbSaved,
            message = $"تم بنجاح رفع وضغط {syncedCount} صورة بصيغة WebP إلى Cloudinary وتحديث قاعدة البيانات بالروابط السحابية!"
        });
    }
}

