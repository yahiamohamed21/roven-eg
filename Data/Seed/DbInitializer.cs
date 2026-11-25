using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockify.Common.Constants;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;
using System.IO;

namespace Stockify.Data.Seed;

public static class DbInitializer
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Seed Roles
        string[] roles = [Roles.Admin, Roles.Employee];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Default Admin User
        const string adminEmail = "admin@roven.local";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail)
                            ?? await userManager.FindByEmailAsync("admin@stockify.local");

        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true,
                Permissions = string.Join(",", Permissions.DefaultAdmin)
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }
        else if (string.IsNullOrEmpty(existingAdmin.Permissions))
        {
            existingAdmin.Permissions = string.Join(",", Permissions.DefaultAdmin);
            await userManager.UpdateAsync(existingAdmin);
        }

        // 3. Seed Default Sales / Employee User
        const string salesEmail = "sales@roven.local";
        var existingSales = await userManager.FindByEmailAsync(salesEmail)
                            ?? await userManager.FindByEmailAsync("sales@stockify.local");

        if (existingSales == null)
        {
            var salesUser = new ApplicationUser
            {
                UserName = salesEmail,
                Email = salesEmail,
                FullName = "Yahia Mohamed (Sales)",
                EmailConfirmed = true,
                IsActive = true,
                Permissions = string.Join(",", Permissions.DefaultSales)
            };

            var result = await userManager.CreateAsync(salesUser, "Sales@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(salesUser, Roles.Employee);
            }
        }
        else if (string.IsNullOrEmpty(existingSales.Permissions))
        {
            existingSales.Permissions = string.Join(",", Permissions.DefaultSales);
            await userManager.UpdateAsync(existingSales);
        }

        // 4. Seed Perfume Collections if not present
        if (!await context.Categories.AnyAsync())
        {
            var catExtrait = new Category { Name = "Extrait de Parfum", Description = "Highest perfume concentration (30%+) offering remarkable longevity and intoxicating sillage." };
            var catEdp = new Category { Name = "Eau de Parfum", Description = "Signature olfactory compositions crafted with rare botanicals, Grasse florals, and precious woods." };
            var catOud = new Category { Name = "Private Oriental & Oud", Description = "Opulent artisanal blends of aged Assam oud, ambergris, frankincense, and spicy saffron." };
            var catSets = new Category { Name = "Discovery & Gift Sets", Description = "Curated luxury presentation boxes and travel atomizers for perfume connoisseurs." };

            context.Categories.AddRange(catExtrait, catEdp, catOud, catSets);
            await context.SaveChangesAsync();
        }

        // 4b. Ensure Winter Collection Category has its campaign image
        var winterCategory = await context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == "winter" || c.Name.ToLower().Contains("winter") || c.Id == 2);
        if (winterCategory != null && string.IsNullOrEmpty(winterCategory.ImageUrl))
        {
            winterCategory.ImageUrl = "/roven/categories/winter.jpg";
            await context.SaveChangesAsync();
        }

        // 5. Seed Egyptian Governorates with Luxury Shipping Rates
        if (!await context.Governorates.AnyAsync())
        {
            var governorates = new List<Governorate>
            {
                new() { NameAr = "القاهرة", NameEn = "Cairo", ShippingFee = 50.00m, DeliveryTime = "1-2 أيام عمل", DisplayOrder = 1 },
                new() { NameAr = "الجيزة", NameEn = "Giza", ShippingFee = 50.00m, DeliveryTime = "1-2 أيام عمل", DisplayOrder = 2 },
                new() { NameAr = "الإسكندرية", NameEn = "Alexandria", ShippingFee = 65.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 3 },
                new() { NameAr = "القليوبية", NameEn = "Qalyubia", ShippingFee = 60.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 4 },
                new() { NameAr = "الشرقية", NameEn = "Sharqia", ShippingFee = 70.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 5 },
                new() { NameAr = "الدقهلية", NameEn = "Dakahlia", ShippingFee = 70.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 6 },
                new() { NameAr = "الغربية", NameEn = "Gharbia", ShippingFee = 70.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 7 },
                new() { NameAr = "المنوفية", NameEn = "Monufia", ShippingFee = 70.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 8 },
                new() { NameAr = "البحيرة", NameEn = "Beheira", ShippingFee = 70.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 9 },
                new() { NameAr = "كفر الشيخ", NameEn = "Kafr El-Sheikh", ShippingFee = 75.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 10 },
                new() { NameAr = "دمياط", NameEn = "Damietta", ShippingFee = 75.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 11 },
                new() { NameAr = "بورسعيد", NameEn = "Port Said", ShippingFee = 75.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 12 },
                new() { NameAr = "الإسماعيلية", NameEn = "Ismailia", ShippingFee = 75.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 13 },
                new() { NameAr = "السويس", NameEn = "Suez", ShippingFee = 75.00m, DeliveryTime = "2-3 أيام عمل", DisplayOrder = 14 },
                new() { NameAr = "الفيوم", NameEn = "Fayoum", ShippingFee = 80.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 15 },
                new() { NameAr = "بني سويف", NameEn = "Beni Suef", ShippingFee = 80.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 16 },
                new() { NameAr = "المنيا", NameEn = "Minya", ShippingFee = 85.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 17 },
                new() { NameAr = "أسيوط", NameEn = "Asyut", ShippingFee = 85.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 18 },
                new() { NameAr = "سوهاج", NameEn = "Sohag", ShippingFee = 90.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 19 },
                new() { NameAr = "قنا", NameEn = "Qena", ShippingFee = 95.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 20 },
                new() { NameAr = "الأقصر", NameEn = "Luxor", ShippingFee = 100.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 21 },
                new() { NameAr = "أسوان", NameEn = "Aswan", ShippingFee = 100.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 22 },
                new() { NameAr = "البحر الأحمر (الغردقة)", NameEn = "Red Sea (Hurghada)", ShippingFee = 110.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 23 },
                new() { NameAr = "جنوب سيناء (شرم الشيخ)", NameEn = "South Sinai (Sharm)", ShippingFee = 120.00m, DeliveryTime = "3-5 أيام عمل", DisplayOrder = 24 },
                new() { NameAr = "شمال سيناء", NameEn = "North Sinai", ShippingFee = 120.00m, DeliveryTime = "3-5 أيام عمل", DisplayOrder = 25 },
                new() { NameAr = "مطروح والساحل الشمالي", NameEn = "Matrouh", ShippingFee = 100.00m, DeliveryTime = "3-4 أيام عمل", DisplayOrder = 26 },
                new() { NameAr = "الوادي الجديد", NameEn = "New Valley", ShippingFee = 120.00m, DeliveryTime = "3-5 أيام عمل", DisplayOrder = 27 }
            };

            context.Governorates.AddRange(governorates);
            await context.SaveChangesAsync();
        }

        // 6. Seed Initial Multi-Product Home Offers (Deals of the Day)
        if (!await context.HomeOffers.AnyAsync())
        {
            var firstProds = await context.Products.Where(p => p.IsActive && p.IsPublished).Take(3).ToListAsync();
            if (firstProds.Count >= 2)
            {
                var offers = new List<HomeOffer>
                {
                    new()
                    {
                        ProductId = firstProds[0].Id,
                        OfferPrice = Math.Round(firstProds[0].Price * 0.85m, 2),
                        BadgeText = "HOT DEAL • عرض مميز",
                        AllocatedPercent = 84,
                        RemainingCount = 18,
                        EndTime = DateTime.UtcNow.AddDays(4),
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new()
                    {
                        ProductId = firstProds[1].Id,
                        OfferPrice = Math.Round(firstProds[1].Price * 0.88m, 2),
                        BadgeText = "EXCLUSIVE • خصم خاص",
                        AllocatedPercent = 76,
                        RemainingCount = 12,
                        EndTime = DateTime.UtcNow.AddDays(5),
                        DisplayOrder = 2,
                        IsActive = true
                    }
                };
                if (firstProds.Count >= 3)
                {
                    offers.Add(new()
                    {
                        ProductId = firstProds[2].Id,
                        OfferPrice = Math.Round(firstProds[2].Price * 0.82m, 2),
                        BadgeText = "LIMITED BATCH • كمية محدودة",
                        AllocatedPercent = 91,
                        RemainingCount = 7,
                        EndTime = DateTime.UtcNow.AddDays(3),
                        DisplayOrder = 3,
                        IsActive = true
                    });
                }
                context.HomeOffers.AddRange(offers);
                await context.SaveChangesAsync();
            }
        }

        // 7. Seed Initial Hero Banners (Full-Image Hero Slides)
        if (!await context.HeroBanners.AnyAsync())
        {
            var initialBanners = new List<HeroBanner>
            {
                new()
                {
                    Title = "Hero Banner Slide #1 • Rouge Velours Extrait",
                    DesktopImageUrl = "/images/banners/roven-hero-main.png",
                    MobileImageUrl = "/images/banners/roven-hero-main-sm.png",
                    RedirectLink = "/Store?categoryId=1",
                    AltText = "Maison ROVEN Rouge Velours Extrait - 30% Pure Oil Concentration",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Title = "Hero Banner Slide #2 • Royal Amber & Assam Oud",
                    DesktopImageUrl = "/images/banners/roven-hero-main.png",
                    MobileImageUrl = "/images/banners/roven-hero-main-sm.png",
                    RedirectLink = "/Store?categoryId=3",
                    AltText = "Maison ROVEN Private Reserve - Aged Assam Agarwood & Golden Resins",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Title = "Hero Banner Slide #3 • The Discovery Coffret",
                    DesktopImageUrl = "/images/banners/roven-hero-main.png",
                    MobileImageUrl = "/images/banners/roven-hero-main-sm.png",
                    RedirectLink = "/Store",
                    AltText = "Maison ROVEN The Grand Discovery Coffret - 5 Bespoke Travel Flacons",
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
            context.HeroBanners.AddRange(initialBanners);
            await context.SaveChangesAsync();
        }
    }

    public static async Task AutoSyncImagesToCloudinaryAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var imageStorageService = serviceProvider.GetRequiredService<IImageStorageService>();
            var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

            if (!imageStorageService.IsConfigured) return;

            var webRoot = !string.IsNullOrWhiteSpace(environment.WebRootPath) 
                ? environment.WebRootPath 
                : Path.Combine(environment.ContentRootPath, "wwwroot");

            bool changed = false;

            // 1. Sync Products Images
            var products = await context.Products
                .Where(p => !string.IsNullOrEmpty(p.ImageUrl) && !p.ImageUrl.StartsWith("http"))
                .ToListAsync();

            foreach (var p in products)
            {
                var cleanPath = p.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(webRoot, cleanPath);
                if (File.Exists(fullLocalPath))
                {
                    await using var stream = File.OpenRead(fullLocalPath);
                    var newUrl = await imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/products");
                    if (!string.IsNullOrWhiteSpace(newUrl))
                    {
                        p.ImageUrl = newUrl;
                        changed = true;
                    }
                }
            }

            // 2. Sync Hero Banners Images
            var banners = await context.HeroBanners
                .Where(b => (!string.IsNullOrEmpty(b.DesktopImageUrl) && !b.DesktopImageUrl.StartsWith("http"))
                         || (!string.IsNullOrEmpty(b.MobileImageUrl) && !b.MobileImageUrl.StartsWith("http")))
                .ToListAsync();

            foreach (var b in banners)
            {
                if (!string.IsNullOrWhiteSpace(b.DesktopImageUrl) && !b.DesktopImageUrl.StartsWith("http"))
                {
                    var cleanPath = b.DesktopImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(webRoot, cleanPath);
                    if (File.Exists(fullLocalPath))
                    {
                        await using var stream = File.OpenRead(fullLocalPath);
                        var newUrl = await imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/banners");
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
                    var fullLocalPath = Path.Combine(webRoot, cleanPath);
                    if (File.Exists(fullLocalPath))
                    {
                        await using var stream = File.OpenRead(fullLocalPath);
                        var newUrl = await imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/banners");
                        if (!string.IsNullOrWhiteSpace(newUrl))
                        {
                            b.MobileImageUrl = newUrl;
                            changed = true;
                        }
                    }
                }
            }

            // 3. Sync Categories Images
            var categories = await context.Categories
                .Where(c => !string.IsNullOrEmpty(c.ImageUrl) && !c.ImageUrl.StartsWith("http"))
                .ToListAsync();

            foreach (var cat in categories)
            {
                var cleanPath = cat.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var fullLocalPath = Path.Combine(webRoot, cleanPath);
                if (File.Exists(fullLocalPath))
                {
                    await using var stream = File.OpenRead(fullLocalPath);
                    var newUrl = await imageStorageService.UploadFileAsync(stream, Path.GetFileName(fullLocalPath), "roven/categories");
                    if (!string.IsNullOrWhiteSpace(newUrl))
                    {
                        cat.ImageUrl = newUrl;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }
        catch
        {
            // Silently ignore startup sync errors to prevent blocking application boot
        }
    }

    /// <summary>
    /// Converts all existing product raster images (.jpg, .jpeg, .png, etc.) to lightweight .webp format
    /// on disk and updates Products.ImageUrl references in the database so server load stays minimal.
    /// </summary>
    public static async Task EnsureProductImagesWebpAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            var cacheService = serviceProvider.GetService<IStoreCacheService>();

            var webRoot = !string.IsNullOrWhiteSpace(environment.WebRootPath)
                ? environment.WebRootPath
                : Path.Combine(environment.ContentRootPath, "wwwroot");

            bool dbChanged = false;

            // 1. Convert product image files referenced in Products table
            var products = await context.Products
                .Where(p => !string.IsNullOrEmpty(p.ImageUrl))
                .ToListAsync();

            foreach (var product in products)
            {
                var url = product.ImageUrl!.Trim();
                if (!Stockify.Common.Media.WebpImageHelper.IsConvertibleRasterExtension(url))
                {
                    continue;
                }

                if (url.Contains("res.cloudinary.com/", StringComparison.OrdinalIgnoreCase))
                {
                    product.ImageUrl = Stockify.Common.Media.WebpImageHelper.ChangeUrlExtensionToWebp(url);
                    dbChanged = true;
                    continue;
                }

                if (url.StartsWith("/"))
                {
                    var cleanRelative = url.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(webRoot, cleanRelative);
                    var webpLocalPath = Path.ChangeExtension(fullLocalPath, ".webp");
                    var newWebpUrl = Stockify.Common.Media.WebpImageHelper.ChangeUrlExtensionToWebp(url);

                    if (File.Exists(webpLocalPath) && new FileInfo(webpLocalPath).Length > 0)
                    {
                        product.ImageUrl = newWebpUrl;
                        dbChanged = true;
                        if (File.Exists(fullLocalPath))
                        {
                            try { File.Delete(fullLocalPath); } catch { }
                        }
                    }
                    else if (File.Exists(fullLocalPath))
                    {
                        var converted = await Stockify.Common.Media.WebpImageHelper.ConvertLocalFileToWebpAsync(
                            fullLocalPath, webpLocalPath, deleteSourceAfterSuccess: true);
                        if (converted)
                        {
                            product.ImageUrl = newWebpUrl;
                            dbChanged = true;
                        }
                    }
                }
            }

            if (dbChanged)
            {
                await context.SaveChangesAsync();
                cacheService?.InvalidateAll();
            }

            // 2. Sweep product image folders for any remaining unconverted raster files
            var productDirs = new List<string>
            {
                Path.Combine(webRoot, "roven", "products"),
                Path.Combine(webRoot, "uploads", "products")
            };

            // Also sync persistent roven_media/products folder on VPS if present
            var parentDir = Directory.GetParent(environment.ContentRootPath)?.FullName;
            if (!string.IsNullOrEmpty(parentDir))
            {
                var persistentProductsDir = Path.Combine(parentDir, "roven_media", "products");
                if (Directory.Exists(persistentProductsDir))
                {
                    productDirs.Add(persistentProductsDir);
                }
            }

            foreach (var dir in productDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (!Stockify.Common.Media.WebpImageHelper.IsConvertibleRasterExtension(file)) continue;
                    var webpTarget = Path.ChangeExtension(file, ".webp");
                    if (File.Exists(webpTarget) && new FileInfo(webpTarget).Length > 0)
                    {
                        try { File.Delete(file); } catch { }
                    }
                    else
                    {
                        await Stockify.Common.Media.WebpImageHelper.ConvertLocalFileToWebpAsync(
                            file, webpTarget, deleteSourceAfterSuccess: true);
                    }
                }
            }
        }
        catch
        {
            // Never block application startup if optional image optimization encounters an issue
        }
    }

    public static async Task SeedFragranceDiscoveryBaselineAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Seed Fragrance Notes Catalog if empty
        if (!await context.FragranceNotes.AnyAsync())
        {
            var notes = new List<FragranceNote>
            {
                // Citrus
                new() { Name = "Bergamot", ArabicName = "برغموت", Category = FragranceNoteCategory.Citrus, DisplayOrder = 1, IsActive = true },
                new() { Name = "Lemon", ArabicName = "ليمون", Category = FragranceNoteCategory.Citrus, DisplayOrder = 2, IsActive = true },
                new() { Name = "Mandarin Orange", ArabicName = "يوسفي", Category = FragranceNoteCategory.Citrus, DisplayOrder = 3, IsActive = true },
                new() { Name = "Grapefruit", ArabicName = "جريب فروت", Category = FragranceNoteCategory.Citrus, DisplayOrder = 4, IsActive = true },
                new() { Name = "Orange", ArabicName = "برتقال", Category = FragranceNoteCategory.Citrus, DisplayOrder = 5, IsActive = true },
                new() { Name = "Neroli", ArabicName = "نيرولي", Category = FragranceNoteCategory.Citrus, DisplayOrder = 6, IsActive = true },
                new() { Name = "Lime", ArabicName = "لايم", Category = FragranceNoteCategory.Citrus, DisplayOrder = 7, IsActive = true },

                // Floral
                new() { Name = "Rose", ArabicName = "ورد", Category = FragranceNoteCategory.Floral, DisplayOrder = 10, IsActive = true },
                new() { Name = "Jasmine", ArabicName = "ياسمين", Category = FragranceNoteCategory.Floral, DisplayOrder = 11, IsActive = true },
                new() { Name = "Lavender", ArabicName = "لافندر", Category = FragranceNoteCategory.Floral, DisplayOrder = 12, IsActive = true },
                new() { Name = "Iris", ArabicName = "سوسن", Category = FragranceNoteCategory.Floral, DisplayOrder = 13, IsActive = true },
                new() { Name = "Orange Blossom", ArabicName = "زهر البرتقال", Category = FragranceNoteCategory.Floral, DisplayOrder = 14, IsActive = true },
                new() { Name = "Violet", ArabicName = "بنفسج", Category = FragranceNoteCategory.Floral, DisplayOrder = 15, IsActive = true },
                new() { Name = "Ylang-Ylang", ArabicName = "يلانج يلانج", Category = FragranceNoteCategory.Floral, DisplayOrder = 16, IsActive = true },

                // Woody
                new() { Name = "Cedar", ArabicName = "خشب الأرز", Category = FragranceNoteCategory.Woody, DisplayOrder = 20, IsActive = true },
                new() { Name = "Sandalwood", ArabicName = "خشب الصندل", Category = FragranceNoteCategory.Woody, DisplayOrder = 21, IsActive = true },
                new() { Name = "Vetiver", ArabicName = "نجيل الهند", Category = FragranceNoteCategory.Woody, DisplayOrder = 22, IsActive = true },
                new() { Name = "Patchouli", ArabicName = "باتشولي", Category = FragranceNoteCategory.Woody, DisplayOrder = 23, IsActive = true },
                new() { Name = "Guaiac Wood", ArabicName = "خشب الغاياك", Category = FragranceNoteCategory.Woody, DisplayOrder = 24, IsActive = true },
                new() { Name = "Cashmere Wood", ArabicName = "خشب الكشمير", Category = FragranceNoteCategory.Woody, DisplayOrder = 25, IsActive = true },
                new() { Name = "Leather", ArabicName = "جلود", Category = FragranceNoteCategory.Woody, DisplayOrder = 26, IsActive = true },

                // Oriental / Amber
                new() { Name = "Amber", ArabicName = "عنبر", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 30, IsActive = true },
                new() { Name = "Oud", ArabicName = "عود", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 31, IsActive = true },
                new() { Name = "Incense", ArabicName = "بخور", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 32, IsActive = true },
                new() { Name = "Saffron", ArabicName = "زعفران", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 33, IsActive = true },
                new() { Name = "Benzoin", ArabicName = "بنزوين", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 34, IsActive = true },
                new() { Name = "Cinnamon", ArabicName = "قرفة", Category = FragranceNoteCategory.OrientalAmber, DisplayOrder = 35, IsActive = true },

                // Fresh / Aromatic
                new() { Name = "Mint", ArabicName = "نعناع", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 40, IsActive = true },
                new() { Name = "Cardamom", ArabicName = "حبهان / هيل", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 41, IsActive = true },
                new() { Name = "Pink Pepper", ArabicName = "فلفل وردي", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 42, IsActive = true },
                new() { Name = "Black Pepper", ArabicName = "فلفل أسود", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 43, IsActive = true },
                new() { Name = "Sage", ArabicName = "مرمرية", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 44, IsActive = true },
                new() { Name = "Ginger", ArabicName = "زنجبيل", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 45, IsActive = true },
                new() { Name = "Apple", ArabicName = "تفاح", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 46, IsActive = true },
                new() { Name = "Nutmeg", ArabicName = "جوزة الطيب", Category = FragranceNoteCategory.FreshAromatic, DisplayOrder = 47, IsActive = true },

                // Gourmand / Sweet
                new() { Name = "Vanilla", ArabicName = "فانيلا", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 50, IsActive = true },
                new() { Name = "Tonka Bean", ArabicName = "فول التونكا", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 51, IsActive = true },
                new() { Name = "Tobacco", ArabicName = "تبغ", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 52, IsActive = true },
                new() { Name = "Honey", ArabicName = "عسل", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 53, IsActive = true },
                new() { Name = "Dark Chocolate", ArabicName = "شوكولاتة داكنة", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 54, IsActive = true },
                new() { Name = "Caramel", ArabicName = "كراميل", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 55, IsActive = true },
                new() { Name = "Coffee", ArabicName = "قهوة", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 56, IsActive = true },
                new() { Name = "Rum", ArabicName = "رم", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 57, IsActive = true },
                new() { Name = "Brown Sugar", ArabicName = "سكر بني", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 58, IsActive = true },
                new() { Name = "White Musk", ArabicName = "مسك أبيض", Category = FragranceNoteCategory.GourmandSweet, DisplayOrder = 59, IsActive = true }
            };

            context.FragranceNotes.AddRange(notes);
            await context.SaveChangesAsync();
        }

        // 2. Seed Quick Picks if empty
        if (!await context.FragranceNoteQuickPicks.AnyAsync())
        {
            var allNotes = await context.FragranceNotes.ToDictionaryAsync(n => n.Name.ToLower(), n => n.Id);

            int order = 1;
            // Loved Notes Quick Picks
            string[] loveNoteNames = ["vanilla", "amber", "bergamot", "lavender", "oud", "rose", "cedar", "cardamom"];
            foreach (var name in loveNoteNames)
            {
                if (allNotes.TryGetValue(name, out var noteId))
                {
                    context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
                    {
                        FragranceNoteId = noteId,
                        Context = QuickPickContext.Love,
                        DisplayOrder = order++
                    });
                }
            }

            // Avoided Notes Quick Picks
            order = 1;
            string[] avoidNoteNames = ["oud", "tobacco", "patchouli", "leather", "incense", "rose"];
            foreach (var name in avoidNoteNames)
            {
                if (allNotes.TryGetValue(name, out var noteId))
                {
                    context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
                    {
                        FragranceNoteId = noteId,
                        Context = QuickPickContext.Avoid,
                        DisplayOrder = order++
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // 3. Seed Baseline Products in LocalDB if Products table is empty
        var defaultCategory = await context.Categories.FirstOrDefaultAsync();
        int categoryId = defaultCategory?.Id ?? 1;

        if (!await context.Products.AnyAsync())
        {
            var baselineProducts = new List<Product>
            {
                new()
                {
                    Sku = "1",
                    Name = "STAR",
                    Description = "Perfume Star – Inspired by Bleu de Chanel. Blends fresh citrus notes with aromatic accords and a refined woody finish.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 550.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/uploads/star_blended.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = true,
                    IsBestSeller = true,
                    Projection = FragranceProjection.Strong,
                    InspiredBy = "Bleu de Chanel",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "2",
                    Name = "VELVET",
                    Description = "Perfume Velvet – Inspired by Grand Soir Maison Francis Kurkdjian. Rich, warm amber with radiant vanilla and sophisticated tonka bean.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 600.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/uploads/velvet_blended.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = true,
                    IsBestSeller = false,
                    Projection = FragranceProjection.Enormous,
                    InspiredBy = "Grand Soir",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "5",
                    Name = "VENOUM",
                    Description = "Perfume Venoum – A seductive, intoxicating dark oriental blend featuring aged rum, smoky tobacco, honey, and roasted coffee.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 800.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/roven/products/venoum.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = false,
                    IsBestSeller = false,
                    Projection = FragranceProjection.Enormous,
                    InspiredBy = "Black Phantom",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "6",
                    Name = "ROYAL",
                    Description = "Perfume Royal – Inspired by Layton. Distinguished blend of crisp apple, bergamot, fresh lavender, warm cardamom, and vanilla.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 600.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/roven/products/royal.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = false,
                    IsBestSeller = false,
                    Projection = FragranceProjection.Strong,
                    InspiredBy = "Layton",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "7",
                    Name = "WANTED",
                    Description = "Perfume Wanted – An irresistible spicy woody scent driven by warm cinnamon, mandarin, cedarwood, and smoldering incense.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 600.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/roven/products/wanted.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = false,
                    IsBestSeller = false,
                    Projection = FragranceProjection.Strong,
                    InspiredBy = "Wanted by Night",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "9",
                    Name = "VANILLA RUSH",
                    Description = "Perfume Vanilla Rush – An opulent, velvety gourmand sensation featuring golden Madagascar vanilla, brown sugar, caramel, and amber.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 800.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/roven/products/vanilla_rush.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = false,
                    IsBestSeller = true,
                    Projection = FragranceProjection.Enormous,
                    InspiredBy = "Vanilla 28 / Bianco Latte",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Sku = "10",
                    Name = "FRUITY ELIXIR",
                    Description = "Perfume Fruity Elixir – Inspired by Erba Pura. A radiant, juicy explosion of Mediterranean citrus, luscious fruits, and pure white musk.",
                    FragranceTagline = "Extrait de Parfum • 30% Pure Concentration",
                    Price = 600.00m,
                    StockQuantity = 10,
                    ReorderLevel = 5,
                    CategoryId = categoryId,
                    ImageUrl = "/roven/products/fruity_elixir.webp",
                    IsActive = true,
                    IsPublished = true,
                    IsFeatured = false,
                    IsBestSeller = false,
                    Projection = FragranceProjection.Moderate,
                    InspiredBy = "Erba Pura",
                    IsFragranceProfileApproved = false,
                    FragranceProfileReviewedAt = null,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Products.AddRange(baselineProducts);
            await context.SaveChangesAsync();
        }

        // 4. Attach Draft Fragrance Profiles to Baseline Products if not yet attached
        var notesDict = await context.FragranceNotes.ToDictionaryAsync(n => n.Name.ToLower(), n => n.Id);

        // Helper to safely assign draft profile by SKU
        async Task AssignDraftProfileAsync(string sku, List<AudienceTarget> audiences, List<FragranceSeason> seasons, List<string> noteNames)
        {
            var prod = await context.Products
                .Include(p => p.TargetAudiences)
                .Include(p => p.Seasons)
                .Include(p => p.ProductFragranceNotes)
                .FirstOrDefaultAsync(p => p.Sku == sku);

            if (prod == null) return;

            // Only attach if no profile joins exist yet (idempotent, never overwrite admin edits)
            if (!prod.TargetAudiences.Any())
            {
                foreach (var aud in audiences)
                {
                    prod.TargetAudiences.Add(new ProductTargetAudience { ProductId = prod.Id, Audience = aud });
                }
            }

            if (!prod.Seasons.Any())
            {
                foreach (var s in seasons)
                {
                    prod.Seasons.Add(new ProductSeason { ProductId = prod.Id, Season = s });
                }
            }

            if (!prod.ProductFragranceNotes.Any())
            {
                foreach (var nName in noteNames)
                {
                    if (notesDict.TryGetValue(nName.ToLower(), out var nId))
                    {
                        prod.ProductFragranceNotes.Add(new ProductFragranceNote
                        {
                            ProductId = prod.Id,
                            FragranceNoteId = nId,
                            AddedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await AssignDraftProfileAsync("1", 
            [AudienceTarget.ForHim], 
            [FragranceSeason.Spring, FragranceSeason.Summer, FragranceSeason.Fall], 
            ["Grapefruit", "Lemon", "Mint", "Ginger", "Nutmeg", "Jasmine", "Incense", "Vetiver", "Cedar", "Sandalwood", "Patchouli"]);

        await AssignDraftProfileAsync("2", 
            [AudienceTarget.Unisex], 
            [FragranceSeason.Fall, FragranceSeason.Winter], 
            ["Amber", "Vanilla", "Tonka Bean", "Benzoin", "Lavender"]);

        await AssignDraftProfileAsync("5", 
            [AudienceTarget.ForHim, AudienceTarget.Unisex], 
            [FragranceSeason.Fall, FragranceSeason.Winter], 
            ["Tobacco", "Honey", "Rum", "Dark Chocolate", "Coffee", "Caramel"]);

        await AssignDraftProfileAsync("6", 
            [AudienceTarget.ForHim], 
            [FragranceSeason.Spring, FragranceSeason.Fall, FragranceSeason.Winter], 
            ["Apple", "Bergamot", "Lavender", "Cardamom", "Vanilla", "Black Pepper", "Patchouli"]);

        await AssignDraftProfileAsync("7", 
            [AudienceTarget.ForHim], 
            [FragranceSeason.Spring, FragranceSeason.Summer, FragranceSeason.Fall], 
            ["Cinnamon", "Mandarin Orange", "Lavender", "Cedar", "Incense"]);

        await AssignDraftProfileAsync("9", 
            [AudienceTarget.ForHer, AudienceTarget.Unisex], 
            [FragranceSeason.Fall, FragranceSeason.Winter], 
            ["Vanilla", "Brown Sugar", "Tonka Bean", "Caramel", "White Musk", "Amber"]);

        await AssignDraftProfileAsync("10", 
            [AudienceTarget.ForHer, AudienceTarget.Unisex], 
            [FragranceSeason.Spring, FragranceSeason.Summer], 
            ["Orange", "Bergamot", "Lemon", "White Musk", "Vanilla"]);

        await context.SaveChangesAsync();
    }
}
