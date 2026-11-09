using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockify.Common.Constants;
using Stockify.Models.Entities;

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
                    DesktopImageUrl = "/images/banners/hero_slide_1.svg",
                    MobileImageUrl = "/images/banners/hero_slide_1_mobile.svg",
                    RedirectLink = "/Store?categoryId=1",
                    AltText = "Maison ROVEN Rouge Velours Extrait - 30% Pure Oil Concentration",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Title = "Hero Banner Slide #2 • Royal Amber & Assam Oud",
                    DesktopImageUrl = "/images/banners/hero_slide_2.svg",
                    MobileImageUrl = "/images/banners/hero_slide_2_mobile.svg",
                    RedirectLink = "/Store?categoryId=3",
                    AltText = "Maison ROVEN Private Reserve - Aged Assam Agarwood & Golden Resins",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Title = "Hero Banner Slide #3 • The Discovery Coffret",
                    DesktopImageUrl = "/images/banners/hero_slide_3.svg",
                    MobileImageUrl = "/images/banners/hero_slide_3_mobile.svg",
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
}
