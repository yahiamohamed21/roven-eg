using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Featured Products (active and published)
        var featured = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsPublished && p.IsFeatured)
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .ToListAsync();

        // If not enough featured, fill with latest products
        if (featured.Count < 4)
        {
            var fallback = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.IsPublished && !featured.Select(f => f.Id).Contains(p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .Take(8 - featured.Count)
                .Select(p => new StoreProductCardViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku ?? "",
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : "General",
                    IsFeatured = p.IsFeatured
                })
                .ToListAsync();

            featured.AddRange(fallback);
        }

        // 2. Latest Arrivals
        var latest = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsPublished)
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .ToListAsync();

        // 3. Best Sellers (Based on completed order volume or high stock products)
        var bestSellers = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsPublished && p.StockQuantity > 0)
            .OrderByDescending(p => p.OrderItems.Count)
            .ThenByDescending(p => p.StockQuantity)
            .Take(4)
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .ToListAsync();

        // 4. Deal of the Day / Special Offers (Supports Single Perfume and Duo / 2 Perfumes Bundle Offers)
        var activeOffers = await _context.HomeOffers
            .Include(o => o.Product)
                .ThenInclude(p => p!.Category)
            .Include(o => o.SecondProduct)
                .ThenInclude(p => p!.Category)
            .Where(o => o.IsActive && o.Product != null && o.Product.IsActive && o.Product.IsPublished)
            .OrderBy(o => o.DisplayOrder)
            .ThenByDescending(o => o.CreatedAt)
            .Select(o => new HomeOfferItemViewModel
            {
                Id = o.Id,
                ProductId = o.ProductId,
                ProductName = o.Product!.Name,
                Sku = o.Product.Sku ?? "",
                CategoryName = o.Product.Category != null ? o.Product.Category.Name : "Haute Parfumerie",
                ImageUrl = o.Product.ImageUrl,
                Description = o.Product.Description,
                FirstProductPrice = o.Product.Price,

                // Second Product Details for 2-Perfume Offers
                SecondProductId = o.SecondProductId,
                SecondProductName = o.SecondProduct != null ? o.SecondProduct.Name : null,
                SecondProductSku = o.SecondProduct != null ? o.SecondProduct.Sku : null,
                SecondProductCategoryName = o.SecondProduct != null && o.SecondProduct.Category != null ? o.SecondProduct.Category.Name : null,
                SecondProductImageUrl = o.SecondProduct != null ? o.SecondProduct.ImageUrl : null,
                SecondProductPrice = o.SecondProduct != null ? o.SecondProduct.Price : null,

                Title = o.Title,
                OfferPrice = o.OfferPrice > 0 ? o.OfferPrice : o.Product.Price,
                BadgeText = o.BadgeText,
                EndTime = o.EndTime,
                AllocatedPercent = o.AllocatedPercent,
                RemainingCount = o.RemainingCount > 0 ? o.RemainingCount : o.Product.StockQuantity,
                IsActive = o.IsActive,
                IsOutOfStock = o.Product.StockQuantity <= 0 || (o.SecondProduct != null && o.SecondProduct.StockQuantity <= 0)
            })
            .ToListAsync();

        var dealOfTheDay = featured.FirstOrDefault(p => !p.IsOutOfStock) ?? latest.FirstOrDefault(p => !p.IsOutOfStock);

        // 5. Categories with published product counts
        var categories = await _context.Categories
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductsCount = c.Products.Count(p => p.IsActive && p.IsPublished)
            })
            .OrderByDescending(c => c.ProductsCount)
            .ToListAsync();

        var totalCatalog = await _context.Products.CountAsync(p => p.IsActive && p.IsPublished);
        var totalCustomers = await _context.Customers.CountAsync();
        var totalCompleted = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Completed);

        var settings = await _context.HomeSettings.FirstOrDefaultAsync() ?? new HomeSetting();

        // If a custom spotlight product is configured in HomeSettings, use it
        if (settings.SpotlightActive && settings.SpotlightProductId.HasValue)
        {
            var customDeal = featured.FirstOrDefault(p => p.Id == settings.SpotlightProductId.Value)
                             ?? latest.FirstOrDefault(p => p.Id == settings.SpotlightProductId.Value);
            if (customDeal != null)
            {
                dealOfTheDay = customDeal;
            }
        }

        // 0. Hero Banners (Full-Image Luxury Slides)
        var heroBanners = await _context.HeroBanners
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();

        var viewModel = new StoreHomeViewModel
        {
            FeaturedProducts = featured,
            LatestProducts = latest,
            BestSellers = bestSellers,
            DealOfTheDay = dealOfTheDay,
            SpecialOffers = activeOffers,
            HeroBanners = heroBanners,
            Categories = categories,
            TotalCatalogCount = totalCatalog,
            TotalCustomersCount = totalCustomers > 0 ? totalCustomers : 1420,
            TotalOrdersCompleted = totalCompleted > 0 ? totalCompleted : 890,
            Settings = settings
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // GET: /Home/About
    public IActionResult About()
    {
        ViewData["Title"] = "About Us — Maison ROVEN Haute Parfumerie";
        ViewData["MetaDescription"] = "Discover the story, legacy, and craftsmanship behind Maison ROVEN Haute Parfumerie. 30%+ Extrait de Parfum, rare Assam oud, and Grasse botanical absolutes.";
        return View();
    }

    // GET: /Home/Contact
    public async Task<IActionResult> Contact()
    {
        ViewData["Title"] = "Contact Us & Concierge — Maison ROVEN";
        ViewData["MetaDescription"] = "Connect with Maison ROVEN fragrance curators. Inquiries about bespoke flacons, private orders, corporate gifting, and order tracking across Egypt.";
        var settings = await _context.HomeSettings.FirstOrDefaultAsync() ?? new HomeSetting();
        return View(settings);
    }

    // POST: /Home/SubmitContact
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitContact(string fullName, string phoneNumber, string? email, string? subject, string message)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(message))
        {
            return Json(new { success = false, message = "يرجى استيفاء الاسم الكامل ورقم الهاتف ونص الرسالة." });
        }

        var contactMsg = new ContactMessage
        {
            FullName = fullName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Subject = string.IsNullOrWhiteSpace(subject) ? "General Inquiry" : subject.Trim(),
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.ContactMessages.Add(contactMsg);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "تم تسجيل رسالتك بنجاح لدى كونسيرج دار روڤين، وسيقوم مستشار العطور بالتواصل معك في أقرب وقت."
        });
    }

    // POST: /Home/SubscribeLead (Storefront VIP Lead Capture & WhatsApp Offers Subscription)
    [HttpPost]
    public async Task<IActionResult> SubscribeLead(string? phone, string? email, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Json(new
            {
                success = false,
                message = "يرجى إدخال رقم الواتساب لاستلام كافة العروض الحصرية."
            });
        }

        var cleanPhone = phone.Trim().Replace(" ", "").Replace("-", "");
        var digitsOnly = System.Text.RegularExpressions.Regex.Replace(cleanPhone, @"\D", "");

        if (digitsOnly.Length < 9 || digitsOnly.Length > 15)
        {
            return Json(new
            {
                success = false,
                message = "يرجى إدخال رقم واتساب صحيح (مثال: 010xxxxxxxx)."
            });
        }

        // 1. Check if customer already exists by phone or match digits
        var matchDigits = digitsOnly.Length >= 10 ? digitsOnly.Substring(digitsOnly.Length - 10) : digitsOnly;
        var existingCustomer = await _context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.PhoneNumber != null && 
                (c.PhoneNumber == cleanPhone || c.PhoneNumber.Replace(" ", "").Replace("-", "").EndsWith(matchDigits)));

        if (existingCustomer != null)
        {
            var hasOrders = existingCustomer.Orders.Any();
            return Json(new
            {
                success = true,
                isNew = false,
                hasOrders = hasOrders,
                phone = cleanPhone,
                customerName = existingCustomer.FullName,
                message = $"أهلاً بك مجدداً في دار روڤين! رقمك مسجل لدينا بالفعل وسيتم إرسال كافة العروض والخصومات الحصرية لرقم الواتساب ({cleanPhone})."
            });
        }

        // 2. Register as a new lead in Customers table (No orders yet)
        var patronName = !string.IsNullOrWhiteSpace(fullName) ? fullName.Trim() : $"عميل واتساب ({cleanPhone})";
        var patronEmail = !string.IsNullOrWhiteSpace(email) && email.Contains("@") 
            ? email.Trim().ToLower() 
            : $"whatsapp_{digitsOnly}@roven.vip";

        var newCustomer = new Customer
        {
            FullName = patronName,
            Email = patronEmail,
            PhoneNumber = cleanPhone,
            Address = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(newCustomer);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            isNew = true,
            hasOrders = false,
            phone = cleanPhone,
            customerName = patronName,
            message = $"تم تسجيل رقمك بنجاح! سيتم إرسال كافة العروض والخصومات الحصرية لرقم الواتساب ({cleanPhone})."
        });
    }

    // GET: /sitemap.xml (Dynamic Search Engine XML Sitemap with Google Image Extensions)
    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap()
    {
        var scheme = Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) ? proto.ToString() : Request.Scheme;
        var host = Request.Headers.TryGetValue("X-Forwarded-Host", out var fHost) ? fHost.ToString() : Request.Host.Value;
        var baseUrl = $"{scheme}://{host}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        // 1. Core Brand Landing Pages
        sb.AppendLine($"  <url><loc>{baseUrl}/</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod><changefreq>daily</changefreq><priority>1.0</priority></url>");
        sb.AppendLine($"  <url><loc>{baseUrl}/Store</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod><changefreq>daily</changefreq><priority>0.9</priority></url>");
        sb.AppendLine($"  <url><loc>{baseUrl}/Home/About</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod><changefreq>monthly</changefreq><priority>0.7</priority></url>");
        sb.AppendLine($"  <url><loc>{baseUrl}/Home/Contact</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod><changefreq>monthly</changefreq><priority>0.7</priority></url>");
        sb.AppendLine($"  <url><loc>{baseUrl}/OrderTracking</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod><changefreq>monthly</changefreq><priority>0.6</priority></url>");

        // 2. Fragrance Categories & Lines
        var categories = await _context.Categories.AsNoTracking().ToListAsync();
        foreach (var cat in categories)
        {
            sb.AppendLine($"  <url><loc>{baseUrl}/Store?categoryId={cat.Id}</loc><lastmod>{cat.CreatedAt:yyyy-MM-dd}</lastmod><changefreq>weekly</changefreq><priority>0.8</priority></url>");
        }

        // 3. Active Perfumes & Flacons (with Google Image Sitemap tags for visual SEO dominance)
        var products = await _context.Products.Where(p => p.IsActive && p.IsPublished).AsNoTracking().ToListAsync();
        foreach (var p in products)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}/Store/Details/{p.Id}</loc>");
            sb.AppendLine($"    <lastmod>{(p.UpdatedAt ?? p.CreatedAt):yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>daily</changefreq>");
            sb.AppendLine("    <priority>0.9</priority>");

            if (!string.IsNullOrWhiteSpace(p.ImageUrl))
            {
                var imgUrl = p.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? p.ImageUrl : $"{baseUrl}{p.ImageUrl}";
                sb.AppendLine("    <image:image>");
                sb.AppendLine($"      <image:loc>{System.Security.SecurityElement.Escape(imgUrl)}</image:loc>");
                sb.AppendLine($"      <image:title>{System.Security.SecurityElement.Escape(p.Name)} - ROVEN Haute Parfumerie</image:title>");
                sb.AppendLine($"      <image:caption>{System.Security.SecurityElement.Escape(p.Name)} عطور روفان ROVEN مصر</image:caption>");
                sb.AppendLine("    </image:image>");
            }
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", System.Text.Encoding.UTF8);
    }

    // GET: /robots.txt (Dynamic Search Engine Robots Protocol with Absolute Sitemap)
    [HttpGet("robots.txt")]
    [ResponseCache(Duration = 86400)]
    public IActionResult RobotsTxt()
    {
        var scheme = Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) ? proto.ToString() : Request.Scheme;
        var host = Request.Headers.TryGetValue("X-Forwarded-Host", out var fHost) ? fHost.ToString() : Request.Host.Value;
        var baseUrl = $"{scheme}://{host}";

        var robots = $@"# ==============================================================================
# ROVEN Haute Parfumerie - Official Robots Configuration for Search Engines
# Brand Domination SEO Configuration
# ==============================================================================

User-agent: *
Allow: /
Allow: /Store
Allow: /Store/Details/
Allow: /OrderTracking
Allow: /Home
Allow: /images/
Allow: /css/
Allow: /js/
Allow: /lib/

# Disallow internal back-office administration from public indexing
Disallow: /Dashboard/
Disallow: /Products/
Disallow: /Categories/
Disallow: /Orders/
Disallow: /Customers/
Disallow: /Users/
Disallow: /Inventory/
Disallow: /Reports/
Disallow: /Account/
Disallow: /Cart/
Disallow: /Checkout/
Disallow: /HomeControl/
Disallow: /AbandonedCheckouts/
Disallow: /Coupons/
Disallow: /Messages/

# Host Directive for Regional Search Engines
Host: {host}

# Dynamic XML Sitemap Index
Sitemap: {baseUrl}/sitemap.xml
";
        return Content(robots, "text/plain", System.Text.Encoding.UTF8);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
