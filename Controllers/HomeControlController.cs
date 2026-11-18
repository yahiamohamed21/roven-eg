using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Services.Interfaces;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class HomeControlController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly Stockify.Services.Interfaces.IImageStorageService _imageStorageService;
    private readonly IStoreCacheService _storeCacheService;
    private readonly IMediaDeletionQueue _mediaDeletionQueue;

    public HomeControlController(
        ApplicationDbContext context, 
        IWebHostEnvironment environment,
        Stockify.Services.Interfaces.IImageStorageService imageStorageService,
        IStoreCacheService storeCacheService,
        IMediaDeletionQueue mediaDeletionQueue)
    {
        _context = context;
        _environment = environment;
        _imageStorageService = imageStorageService;
        _storeCacheService = storeCacheService;
        _mediaDeletionQueue = mediaDeletionQueue;
    }

    private void InvalidateStoreCache()
    {
        _storeCacheService.InvalidateAll();
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
        else
        {
            bool needsSave = false;
            if (string.IsNullOrWhiteSpace(settings.SpotlightBadgeText))
            {
                settings.SpotlightBadgeText = "EXCLUSIVE ATELIER OFFERS & DEALS";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.SpotlightTitle))
            {
                settings.SpotlightTitle = "عروض وتخفيضات دار Roven الحصرية";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.SpotlightSubtitle))
            {
                settings.SpotlightSubtitle = "إصدارات عطرية مختارة بتخفيضات استثنائية لفترة محدودة، مع شحن لكافة المحافظات وإمكانية معاينة الطرد قبل الدفع عند الاستلام.";
                needsSave = true;
            }

            // Ensure section display orders are initialized with default sequence (1 to 11)
            if (settings.OrderBrandHero <= 0) { settings.OrderBrandHero = 1; needsSave = true; }
            if (settings.OrderMarquee <= 0) { settings.OrderMarquee = 2; needsSave = true; }
            if (settings.OrderTrustBadges <= 0) { settings.OrderTrustBadges = 3; needsSave = true; }
            if (settings.OrderHeroBanners <= 0) { settings.OrderHeroBanners = 4; needsSave = true; }
            if (settings.OrderCategories <= 0) { settings.OrderCategories = 5; needsSave = true; }
            if (settings.OrderOffers <= 0) { settings.OrderOffers = 6; needsSave = true; }
            if (settings.OrderFeatured <= 0) { settings.OrderFeatured = 7; needsSave = true; }
            if (settings.OrderBestSellers <= 0) { settings.OrderBestSellers = 8; needsSave = true; }
            if (settings.OrderTestimonials <= 0) { settings.OrderTestimonials = 9; needsSave = true; }
            if (settings.OrderFaq <= 0) { settings.OrderFaq = 10; needsSave = true; }
            if (settings.OrderNewsletter <= 0) { settings.OrderNewsletter = 11; needsSave = true; }

            // Ensure Main Brand Hero properties are initialized with approved defaults
            if (string.IsNullOrWhiteSpace(settings.BrandHeroDesktopImageUrl))
            {
                settings.BrandHeroDesktopImageUrl = "/images/banners/roven-hero-main.png";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.BrandHeroMobileImageUrl))
            {
                settings.BrandHeroMobileImageUrl = "/images/banners/roven-hero-main-sm.png";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.BrandHeroRedirectLink))
            {
                settings.BrandHeroRedirectLink = "/Store";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.BrandHeroAltText))
            {
                settings.BrandHeroAltText = "ROVEN FRAGRANCES — Your Safest Choice • Explore the Collection";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.BrandHeroTitle))
            {
                settings.BrandHeroTitle = "ROVEN FRAGRANCES — Your Safest Choice";
                needsSave = true;
            }

            // Ensure FAQ section customization properties are initialized
            if (string.IsNullOrWhiteSpace(settings.FaqBadgeText))
            {
                settings.FaqBadgeText = "Maison ROVEN Clarifications";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.FaqTitle))
            {
                settings.FaqTitle = "Frequently Asked Questions";
                needsSave = true;
            }
            if (string.IsNullOrWhiteSpace(settings.FaqSubtitle))
            {
                settings.FaqSubtitle = "Everything you need to know about concentrations, parcel inspection, and authenticity";
                needsSave = true;
            }

            if (needsSave)
            {
                await _context.SaveChangesAsync();
            }
        }

        // Auto-seed default FAQ items if table is empty
        if (!await _context.HomeFaqItems.AnyAsync())
        {
            _context.HomeFaqItems.AddRange(
                new HomeFaqItem
                {
                    Question = "What distinguishes an Extrait de Parfum from Eau de Parfum?",
                    Answer = "ROVEN Extraits de Parfum contain <strong>30% or higher</strong> concentrations of pure perfume oil essences, offering an exceptionally rich and intimate sillage that endures for 24+ hours on fabrics and skin. Our Eaux de Parfum are concentrated at 20%, engineered for vibrant projection and refined daytime versatility.",
                    IconClass = "fa-solid fa-droplet",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new HomeFaqItem
                {
                    Question = "How does Cash on Delivery (COD) inspection work?",
                    Answer = "No online credit card is required. You can complete your order instantly as a guest without creating an account. When the courier arrives, you have the right to inspect the parcel seal and external luxury packaging condition before handing over the payment in Egyptian Pounds (EGP).",
                    IconClass = "fa-solid fa-shield-halved",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new HomeFaqItem
                {
                    Question = "What is the expected delivery timeframe?",
                    Answer = "Orders placed for Cairo and Giza are fulfilled within <strong>24 to 48 hours</strong>. Shipments to Alexandria, Delta, and Upper Egypt governorates are fulfilled within <strong>48 to 72 hours</strong> with real-time status updates and SMS notification.",
                    IconClass = "fa-solid fa-truck-fast",
                    DisplayOrder = 3,
                    IsActive = true
                },
                new HomeFaqItem
                {
                    Question = "How can I track my fragrance delivery?",
                    Answer = "Upon order confirmation, you receive a distinct tracking reference number (e.g. <code>ORD-20261105-XXXX</code>). Visit the <a href=\"/OrderTracking\" style=\"color: #630000; font-weight: 700; text-decoration: underline;\">Track Order</a> page anytime to view the live multi-stage dispatch timeline of your flacon.",
                    IconClass = "fa-solid fa-box-open",
                    DisplayOrder = 4,
                    IsActive = true
                }
            );
            await _context.SaveChangesAsync();
        }

        ViewBag.FaqItems = await _context.HomeFaqItems
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Id)
            .ToListAsync();

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

        var selectedIds = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(settings.BestSellersProductIds))
        {
            foreach (var item in settings.BestSellersProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(item, out var id)) selectedIds.Add(id);
            }
        }
        else
        {
            // If not set yet, check if any products have IsBestSeller flag
            var bestSellerProducts = await _context.Products
                .Where(p => p.IsActive && p.IsPublished && p.IsBestSeller)
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var id in bestSellerProducts) selectedIds.Add(id);
        }
        ViewBag.SelectedBestSellerIds = selectedIds;

        if (string.IsNullOrWhiteSpace(settings.FeaturedBadgeText))
            settings.FeaturedBadgeText = "Master Distillations";
        if (string.IsNullOrWhiteSpace(settings.FeaturedTitle))
            settings.FeaturedTitle = "Featured ROVEN Parfums";
        if (string.IsNullOrWhiteSpace(settings.FeaturedSubtitle))
            settings.FeaturedSubtitle = "Our most acclaimed fragrant creations formulated with precious Grasse and oriental oils";
        if (string.IsNullOrWhiteSpace(settings.FeaturedExploreText))
            settings.FeaturedExploreText = "Explore Full Collection";

        var selectedFeaturedIds = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(settings.FeaturedProductIds))
        {
            foreach (var item in settings.FeaturedProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(item, out var id)) selectedFeaturedIds.Add(id);
            }
        }
        else
        {
            // If not set yet, check if any products have IsFeatured flag
            var featuredProducts = await _context.Products
                .Where(p => p.IsActive && p.IsPublished && p.IsFeatured)
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var id in featuredProducts) selectedFeaturedIds.Add(id);
        }
        ViewBag.SelectedFeaturedProductIds = selectedFeaturedIds;

        return View(settings);
    }

    #region Hero Banners Management (Full-Image Hero Slides)

    // POST: /HomeControl/UpdateMainBrandHero
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMainBrandHero(
        IFormFile? desktopImage,
        string? desktopImageUrl,
        IFormFile? mobileImage,
        string? mobileImageUrl,
        string? redirectLink,
        string? altText,
        string? title,
        bool isActive = true)
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
        }

        // 1. Process Desktop Image
        if (desktopImage != null && desktopImage.Length > 0)
        {
            var uploadedDesktop = await SaveBannerFileAsync(desktopImage, "brand_hero_desktop");
            if (!string.IsNullOrWhiteSpace(uploadedDesktop))
            {
                settings.BrandHeroDesktopImageUrl = uploadedDesktop;
            }
        }
        else if (!string.IsNullOrWhiteSpace(desktopImageUrl))
        {
            settings.BrandHeroDesktopImageUrl = desktopImageUrl.Trim();
        }

        // 2. Process Mobile Image
        if (mobileImage != null && mobileImage.Length > 0)
        {
            var uploadedMobile = await SaveBannerFileAsync(mobileImage, "brand_hero_mobile");
            if (!string.IsNullOrWhiteSpace(uploadedMobile))
            {
                settings.BrandHeroMobileImageUrl = uploadedMobile;
            }
        }
        else if (!string.IsNullOrWhiteSpace(mobileImageUrl))
        {
            settings.BrandHeroMobileImageUrl = mobileImageUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(redirectLink))
        {
            settings.BrandHeroRedirectLink = redirectLink.Trim();
        }

        if (!string.IsNullOrWhiteSpace(altText))
        {
            settings.BrandHeroAltText = altText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            settings.BrandHeroTitle = title.Trim();
        }

        settings.ShowBrandHero = isActive;

        await _context.SaveChangesAsync();
        await AutoSyncLocalImagesToCloudinaryAsync();

        InvalidateStoreCache();

        TempData["SuccessMessage"] = "تم تحديث صور وإعدادات بانر الهيرو الأساسي للدار (الديسكتوب والموبايل) بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/ResetMainBrandHero
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetMainBrandHero()
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            settings.BrandHeroDesktopImageUrl = "/images/banners/roven-hero-main.png";
            settings.BrandHeroMobileImageUrl = "/images/banners/roven-hero-main-sm.png";
            settings.BrandHeroRedirectLink = "/Store";
            settings.BrandHeroAltText = "ROVEN FRAGRANCES — Your Safest Choice • Explore the Collection";
            settings.BrandHeroTitle = "ROVEN FRAGRANCES — Your Safest Choice";
            settings.ShowBrandHero = true;

            await _context.SaveChangesAsync();
            InvalidateStoreCache();
        }

        TempData["SuccessMessage"] = "تمت استعادة صور وإعدادات بانر الهيرو الأساسي الافتراضية بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveBannerFileAsync(IFormFile? file, string prefix)
    {
        if (file == null || file.Length == 0) return null;

        if (!Stockify.Common.Security.ImageSecurityValidator.Validate(file, out var error, allowSvg: false, maxSizeBytes: 10 * 1024 * 1024))
        {
            TempData["ErrorMessage"] = error ?? "الملف المرفوع للبانر غير صالح.";
            return null;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
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
            if (TempData["ErrorMessage"] == null)
            {
                TempData["ErrorMessage"] = "يرجى رفع صورة الديسكتوب للبانر (Desktop Image) أو إدخال مسارها.";
            }
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
        await AutoSyncLocalImagesToCloudinaryAsync();

        InvalidateStoreCache();

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

        var oldDesktopUrl = banner.DesktopImageUrl;
        var oldMobileUrl = banner.MobileImageUrl;

        string? newDesktop = null;
        if (desktopImage != null && desktopImage.Length > 0)
        {
            newDesktop = await SaveBannerFileAsync(desktopImage, "desktop");
            if (!string.IsNullOrWhiteSpace(newDesktop))
            {
                banner.DesktopImageUrl = newDesktop;
            }
        }
        else if (!string.IsNullOrWhiteSpace(desktopImageUrl))
        {
            banner.DesktopImageUrl = desktopImageUrl.Trim();
        }

        string? newMobile = null;
        if (mobileImage != null && mobileImage.Length > 0)
        {
            newMobile = await SaveBannerFileAsync(mobileImage, "mobile");
            if (!string.IsNullOrWhiteSpace(newMobile))
            {
                banner.MobileImageUrl = newMobile;
            }
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
        await AutoSyncLocalImagesToCloudinaryAsync();

        InvalidateStoreCache();

        // Safe cleanup for replaced desktop image via background deletion queue
        if (!string.IsNullOrWhiteSpace(newDesktop) &&
            !string.IsNullOrWhiteSpace(oldDesktopUrl) &&
            !string.Equals(oldDesktopUrl, newDesktop, StringComparison.OrdinalIgnoreCase) &&
            oldDesktopUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(oldDesktopUrl);
        }

        // Safe cleanup for replaced mobile image via background deletion queue
        if (!string.IsNullOrWhiteSpace(newMobile) &&
            !string.IsNullOrWhiteSpace(oldMobileUrl) &&
            !string.Equals(oldMobileUrl, newMobile, StringComparison.OrdinalIgnoreCase) &&
            oldMobileUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(oldMobileUrl);
        }

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

        InvalidateStoreCache();

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
            InvalidateStoreCache();
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
            InvalidateStoreCache();
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
        var desktopUrl = banner.DesktopImageUrl;
        var mobileUrl = banner.MobileImageUrl;

        _context.HeroBanners.Remove(banner);
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        // Safe cleanup for deleted banner images via background deletion queue
        if (!string.IsNullOrWhiteSpace(desktopUrl) && desktopUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(desktopUrl);
        }

        if (!string.IsNullOrWhiteSpace(mobileUrl) && mobileUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(mobileUrl);
        }

        TempData["SuccessMessage"] = $"تم حذف البانر '{title}' بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    #endregion

    // POST: /HomeControl/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(HomeSetting model, int[]? selectedBestSellerIds, int[]? selectedFeaturedProductIds)
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
        }

        settings.AnnouncementText = model.AnnouncementText?.Trim() ?? "";
        settings.AnnouncementActive = model.AnnouncementActive;
        settings.DefaultFragranceTagline = !string.IsNullOrWhiteSpace(model.DefaultFragranceTagline) ? model.DefaultFragranceTagline.Trim() : "Extrait de Parfum • 30% Pure Oil";

        // Best Sellers Section Customization
        settings.BestSellersBadgeText = !string.IsNullOrWhiteSpace(model.BestSellersBadgeText) ? model.BestSellersBadgeText.Trim() : "Most Requested";
        settings.BestSellersTitle = !string.IsNullOrWhiteSpace(model.BestSellersTitle) ? model.BestSellersTitle.Trim() : "Top Acclaimed ROVEN Parfums";
        settings.BestSellersSubtitle = !string.IsNullOrWhiteSpace(model.BestSellersSubtitle) ? model.BestSellersSubtitle.Trim() : "The fragrances most cherished by our clientele across Egypt this month";

        if (selectedBestSellerIds != null && selectedBestSellerIds.Length > 0)
        {
            settings.BestSellersProductIds = string.Join(",", selectedBestSellerIds);

            // Synchronize Product.IsBestSeller
            var allProds = await _context.Products.ToListAsync();
            foreach (var prod in allProds)
            {
                prod.IsBestSeller = selectedBestSellerIds.Contains(prod.Id);
            }
        }
        else if (selectedBestSellerIds != null && selectedBestSellerIds.Length == 0)
        {
            settings.BestSellersProductIds = "";
            var bestSellerProds = await _context.Products.Where(p => p.IsBestSeller).ToListAsync();
            foreach (var prod in bestSellerProds)
            {
                prod.IsBestSeller = false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.BestSellersProductIds))
        {
            settings.BestSellersProductIds = model.BestSellersProductIds.Trim();
        }

        // Featured Section Customization
        settings.FeaturedBadgeText = !string.IsNullOrWhiteSpace(model.FeaturedBadgeText) ? model.FeaturedBadgeText.Trim() : "Master Distillations";
        settings.FeaturedTitle = !string.IsNullOrWhiteSpace(model.FeaturedTitle) ? model.FeaturedTitle.Trim() : "Featured ROVEN Parfums";
        settings.FeaturedSubtitle = !string.IsNullOrWhiteSpace(model.FeaturedSubtitle) ? model.FeaturedSubtitle.Trim() : "Our most acclaimed fragrant creations formulated with precious Grasse and oriental oils";
        settings.FeaturedExploreText = !string.IsNullOrWhiteSpace(model.FeaturedExploreText) ? model.FeaturedExploreText.Trim() : "Explore Full Collection";

        if (selectedFeaturedProductIds != null && selectedFeaturedProductIds.Length > 0)
        {
            settings.FeaturedProductIds = string.Join(",", selectedFeaturedProductIds);

            // Synchronize Product.IsFeatured
            var allProds = await _context.Products.ToListAsync();
            foreach (var prod in allProds)
            {
                prod.IsFeatured = selectedFeaturedProductIds.Contains(prod.Id);
            }
        }
        else if (selectedFeaturedProductIds != null && selectedFeaturedProductIds.Length == 0)
        {
            settings.FeaturedProductIds = "";
            var featuredProds = await _context.Products.Where(p => p.IsFeatured).ToListAsync();
            foreach (var prod in featuredProds)
            {
                prod.IsFeatured = false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.FeaturedProductIds))
        {
            settings.FeaturedProductIds = model.FeaturedProductIds.Trim();
        }

        settings.HeroTitle = model.HeroTitle?.Trim() ?? "";
        settings.HeroSubtitle = model.HeroSubtitle?.Trim() ?? "";
        settings.HeroBadgeText = model.HeroBadgeText?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(model.BrandHeroDesktopImageUrl))
            settings.BrandHeroDesktopImageUrl = model.BrandHeroDesktopImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(model.BrandHeroMobileImageUrl))
            settings.BrandHeroMobileImageUrl = model.BrandHeroMobileImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(model.BrandHeroRedirectLink))
            settings.BrandHeroRedirectLink = model.BrandHeroRedirectLink.Trim();
        if (!string.IsNullOrWhiteSpace(model.BrandHeroAltText))
            settings.BrandHeroAltText = model.BrandHeroAltText.Trim();
        if (!string.IsNullOrWhiteSpace(model.BrandHeroTitle))
            settings.BrandHeroTitle = model.BrandHeroTitle.Trim();

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

        settings.SpotlightBadgeText = !string.IsNullOrWhiteSpace(model.SpotlightBadgeText) 
            ? model.SpotlightBadgeText.Trim() 
            : "EXCLUSIVE ATELIER OFFERS & DEALS";
        settings.SpotlightTitle = !string.IsNullOrWhiteSpace(model.SpotlightTitle) 
            ? model.SpotlightTitle.Trim() 
            : "عروض وتخفيضات دار Roven الحصرية";
        settings.SpotlightSubtitle = !string.IsNullOrWhiteSpace(model.SpotlightSubtitle) 
            ? model.SpotlightSubtitle.Trim() 
            : "إصدارات عطرية مختارة بتخفيضات استثنائية لفترة محدودة، مع شحن لكافة المحافظات وإمكانية معاينة الطرد قبل الدفع عند الاستلام.";

        settings.FaqBadgeText = !string.IsNullOrWhiteSpace(model.FaqBadgeText) ? model.FaqBadgeText.Trim() : "Maison ROVEN Clarifications";
        settings.FaqTitle = !string.IsNullOrWhiteSpace(model.FaqTitle) ? model.FaqTitle.Trim() : "Frequently Asked Questions";
        settings.FaqSubtitle = !string.IsNullOrWhiteSpace(model.FaqSubtitle) ? model.FaqSubtitle.Trim() : "Everything you need to know about concentrations, parcel inspection, and authenticity";

        settings.MarqueeActive = model.MarqueeActive;
        settings.MarqueeText = model.MarqueeText?.Trim() ?? "";
        settings.MarqueeBadgeText = model.MarqueeBadgeText?.Trim() ?? "";
        settings.MarqueeSpeedSeconds = model.MarqueeSpeedSeconds > 0 ? model.MarqueeSpeedSeconds : 25;

        // Note: Social media, WhatsApp and Contact info are managed exclusively via MessagesController

        settings.ShowBrandHero = model.ShowBrandHero;
        settings.ShowHeroBanners = model.ShowHeroBanners;
        settings.ShowTestimonialsSection = model.ShowTestimonialsSection;
        settings.ShowTrustBadges = model.ShowTrustBadges;
        settings.ShowCategoriesShowcase = model.ShowCategoriesShowcase;
        settings.ShowFeaturedSection = model.ShowFeaturedSection;
        settings.ShowBestSellersSection = model.ShowBestSellersSection;
        settings.ShowFaqSection = model.ShowFaqSection;
        settings.ShowNewsletterSection = model.ShowNewsletterSection;

        // Section Display Order
        settings.OrderBrandHero = model.OrderBrandHero > 0 ? model.OrderBrandHero : 1;
        settings.OrderMarquee = model.OrderMarquee > 0 ? model.OrderMarquee : 2;
        settings.OrderTrustBadges = model.OrderTrustBadges > 0 ? model.OrderTrustBadges : 3;
        settings.OrderHeroBanners = model.OrderHeroBanners > 0 ? model.OrderHeroBanners : 4;
        settings.OrderCategories = model.OrderCategories > 0 ? model.OrderCategories : 5;
        settings.OrderOffers = model.OrderOffers > 0 ? model.OrderOffers : 6;
        settings.OrderFeatured = model.OrderFeatured > 0 ? model.OrderFeatured : 7;
        settings.OrderBestSellers = model.OrderBestSellers > 0 ? model.OrderBestSellers : 8;
        settings.OrderTestimonials = model.OrderTestimonials > 0 ? model.OrderTestimonials : 9;
        settings.OrderFaq = model.OrderFaq > 0 ? model.OrderFaq : 10;
        settings.OrderNewsletter = model.OrderNewsletter > 0 ? model.OrderNewsletter : 11;

        await _context.SaveChangesAsync();
        await AutoSyncLocalImagesToCloudinaryAsync();

        InvalidateStoreCache();

        TempData["SuccessMessage"] = "تم تحديث وتثبيت إعدادات الواجهة الرئيسية بنجاح!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /HomeControl/UpdateWhatsAppDirect
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWhatsAppDirect([FromForm] string whatsAppNumber)
    {
        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new HomeSetting();
            _context.HomeSettings.Add(settings);
        }

        settings.WhatsAppNumber = whatsAppNumber?.Trim() ?? "";
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        var normalized = HomeSetting.NormalizeWhatsAppNumber(settings.WhatsAppNumber);
        return Json(new 
        { 
            success = true, 
            raw = settings.WhatsAppNumber, 
            normalized = normalized, 
            message = "تم حفظ وتفعيل رقم الواتساب بنجاح، وتم ربطه فورياً بكافة أزرار الطلب في صفحات تفاصيل العطور والفوتر!" 
        });
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

        InvalidateStoreCache();

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

        InvalidateStoreCache();

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

        InvalidateStoreCache();

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

        InvalidateStoreCache();

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
        InvalidateStoreCache();

        var mbSaved = Math.Round((double)bytesSaved / (1024 * 1024), 2);
        return Json(new
        {
            success = true,
            syncedCount = syncedCount,
            mbSaved = mbSaved,
            message = $"تم بنجاح رفع وضغط {syncedCount} صورة بصيغة WebP إلى Cloudinary وتحديث قاعدة البيانات بالروابط السحابية!"
        });
    }

    private async Task AutoSyncLocalImagesToCloudinaryAsync()
    {
        if (!_imageStorageService.IsConfigured) return;

        try
        {
            bool changed = false;

            // 1. Sync Products Images
            var products = await _context.Products
                .Where(p => !string.IsNullOrEmpty(p.ImageUrl) && !p.ImageUrl.StartsWith("http"))
                .ToListAsync();

            foreach (var p in products)
            {
                var cleanPath = p.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                if (System.IO.File.Exists(fullLocalPath))
                {
                    await using var stream = System.IO.File.OpenRead(fullLocalPath);
                    var newUrl = await _imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/products");
                    if (!string.IsNullOrWhiteSpace(newUrl))
                    {
                        p.ImageUrl = newUrl;
                        changed = true;
                    }
                }
            }

            // 2. Sync Hero Banners Images
            var banners = await _context.HeroBanners
                .Where(b => (!string.IsNullOrEmpty(b.DesktopImageUrl) && !b.DesktopImageUrl.StartsWith("http"))
                         || (!string.IsNullOrEmpty(b.MobileImageUrl) && !b.MobileImageUrl.StartsWith("http")))
                .ToListAsync();

            foreach (var b in banners)
            {
                if (!string.IsNullOrWhiteSpace(b.DesktopImageUrl) && !b.DesktopImageUrl.StartsWith("http"))
                {
                    var cleanPath = b.DesktopImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                    if (System.IO.File.Exists(fullLocalPath))
                    {
                        await using var stream = System.IO.File.OpenRead(fullLocalPath);
                        var newUrl = await _imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/banners");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            b.DesktopImageUrl = newUrl;
                            changed = true;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(b.MobileImageUrl) && !b.MobileImageUrl.StartsWith("http"))
                {
                    var cleanPath = b.MobileImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                    if (System.IO.File.Exists(fullLocalPath))
                    {
                        await using var stream = System.IO.File.OpenRead(fullLocalPath);
                        var newUrl = await _imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/banners");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            b.MobileImageUrl = newUrl;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            // Fail-safe: do not interrupt user operations if network or Cloudinary is unreachable
        }
    }

    #region FAQ Management

    // POST: /HomeControl/AddFaqItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFaqItem(string question, string answer, string? iconClass, int displayOrder, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            TempData["ErrorMessage"] = "يرجى كتابة نص السؤال والإجابة بشكل صحيح.";
            return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
        }

        var faqCount = await _context.HomeFaqItems.CountAsync();
        var item = new HomeFaqItem
        {
            Question = question.Trim(),
            Answer = answer.Trim(),
            IconClass = string.IsNullOrWhiteSpace(iconClass) ? "fa-solid fa-circle-question" : iconClass.Trim(),
            DisplayOrder = displayOrder > 0 ? displayOrder : faqCount + 1,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.HomeFaqItems.Add(item);
        await _context.SaveChangesAsync();
        InvalidateStoreCache();

        TempData["SuccessMessage"] = "تمت إضافة السؤال الجديد بنجاح!";
        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    // POST: /HomeControl/UpdateFaqItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFaqItem(int id, string question, string answer, string? iconClass, int displayOrder, bool isActive)
    {
        var item = await _context.HomeFaqItems.FindAsync(id);
        if (item == null)
        {
            TempData["ErrorMessage"] = "السؤال المطلوب غير موجود.";
            return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
        }

        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            TempData["ErrorMessage"] = "لا يمكن ترك نص السؤال أو الإجابة فارغاً.";
            return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
        }

        item.Question = question.Trim();
        item.Answer = answer.Trim();
        item.IconClass = string.IsNullOrWhiteSpace(iconClass) ? "fa-solid fa-circle-question" : iconClass.Trim();
        item.DisplayOrder = displayOrder > 0 ? displayOrder : item.DisplayOrder;
        item.IsActive = isActive;

        await _context.SaveChangesAsync();
        InvalidateStoreCache();

        TempData["SuccessMessage"] = "تم تحديث بيانات السؤال بنجاح!";
        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    // POST: /HomeControl/DeleteFaqItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFaqItem(int id)
    {
        var item = await _context.HomeFaqItems.FindAsync(id);
        if (item != null)
        {
            _context.HomeFaqItems.Remove(item);
            await _context.SaveChangesAsync();
            InvalidateStoreCache();
            TempData["SuccessMessage"] = "تم حذف السؤال بنجاح!";
        }
        else
        {
            TempData["ErrorMessage"] = "السؤال المطلوب غير موجود.";
        }
        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    // POST: /HomeControl/ToggleFaqItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFaqItem(int id)
    {
        var item = await _context.HomeFaqItems.FindAsync(id);
        if (item != null)
        {
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            InvalidateStoreCache();
            TempData["SuccessMessage"] = $"تم {(item.IsActive ? "تفعيل" : "إلغاء تفعيل")} السؤال بنجاح!";
        }
        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    // POST: /HomeControl/MoveFaqItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveFaqItem(int id, string direction)
    {
        var items = await _context.HomeFaqItems
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Id)
            .ToListAsync();

        var index = items.FindIndex(f => f.Id == id);
        if (index >= 0)
        {
            if (direction == "up" && index > 0)
            {
                var prev = items[index - 1];
                var cur = items[index];
                (prev.DisplayOrder, cur.DisplayOrder) = (cur.DisplayOrder, prev.DisplayOrder);
            }
            else if (direction == "down" && index < items.Count - 1)
            {
                var next = items[index + 1];
                var cur = items[index];
                (next.DisplayOrder, cur.DisplayOrder) = (cur.DisplayOrder, next.DisplayOrder);
            }

            // Normalize orders
            for (int i = 0; i < items.Count; i++)
            {
                items[i].DisplayOrder = i + 1;
            }

            await _context.SaveChangesAsync();
            InvalidateStoreCache();
        }

        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    // POST: /HomeControl/ResetDefaultFaqItems
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDefaultFaqItems()
    {
        _context.HomeFaqItems.RemoveRange(await _context.HomeFaqItems.ToListAsync());
        await _context.SaveChangesAsync();

        _context.HomeFaqItems.AddRange(
            new HomeFaqItem
            {
                Question = "What distinguishes an Extrait de Parfum from Eau de Parfum?",
                Answer = "ROVEN Extraits de Parfum contain <strong>30% or higher</strong> concentrations of pure perfume oil essences, offering an exceptionally rich and intimate sillage that endures for 24+ hours on fabrics and skin. Our Eaux de Parfum are concentrated at 20%, engineered for vibrant projection and refined daytime versatility.",
                IconClass = "fa-solid fa-droplet",
                DisplayOrder = 1,
                IsActive = true
            },
            new HomeFaqItem
            {
                Question = "How does Cash on Delivery (COD) inspection work?",
                Answer = "No online credit card is required. You can complete your order instantly as a guest without creating an account. When the courier arrives, you have the right to inspect the parcel seal and external luxury packaging condition before handing over the payment in Egyptian Pounds (EGP).",
                IconClass = "fa-solid fa-shield-halved",
                DisplayOrder = 2,
                IsActive = true
            },
            new HomeFaqItem
            {
                Question = "What is the expected delivery timeframe?",
                Answer = "Orders placed for Cairo and Giza are fulfilled within <strong>24 to 48 hours</strong>. Shipments to Alexandria, Delta, and Upper Egypt governorates are fulfilled within <strong>48 to 72 hours</strong> with real-time status updates and SMS notification.",
                IconClass = "fa-solid fa-truck-fast",
                DisplayOrder = 3,
                IsActive = true
            },
            new HomeFaqItem
            {
                Question = "How can I track my fragrance delivery?",
                Answer = "Upon order confirmation, you receive a distinct tracking reference number (e.g. <code>ORD-20261105-XXXX</code>). Visit the <a href=\"/OrderTracking\" style=\"color: #630000; font-weight: 700; text-decoration: underline;\">Track Order</a> page anytime to view the live multi-stage dispatch timeline of your flacon.",
                IconClass = "fa-solid fa-box-open",
                DisplayOrder = 4,
                IsActive = true
            }
        );

        var settings = await _context.HomeSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            settings.FaqBadgeText = "Maison ROVEN Clarifications";
            settings.FaqTitle = "Frequently Asked Questions";
            settings.FaqSubtitle = "Everything you need to know about concentrations, parcel inspection, and authenticity";
        }

        await _context.SaveChangesAsync();
        InvalidateStoreCache();

        TempData["SuccessMessage"] = "تمت استعادة الأسئلة الشائعة الافتراضية للدار بنجاح!";
        return RedirectToAction(nameof(Index), new { tab = "tab-faq" });
    }

    #endregion
}

