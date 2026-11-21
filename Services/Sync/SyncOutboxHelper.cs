using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Stockify.Data;
using Stockify.DTOs.CatalogSync;
using Stockify.Models.Entities;
using Stockify.Models.Entities.Sync;

namespace Stockify.Services.Sync;

public static class SyncOutboxHelper
{
    public static readonly AsyncLocal<bool> SuppressOutboxScope = new();

    public static List<SyncOutboxEvent> GenerateOutboxEvents(ChangeTracker changeTracker, ApplicationDbContext context)
    {
        var events = new List<SyncOutboxEvent>();

        var productEntries = changeTracker.Entries<Product>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in productEntries)
        {
            var product = entry.Entity;
            string op = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(op)) continue;

            if (entry.State == EntityState.Modified)
            {
                // Check if any commercial property changed
                var commercialProps = new[]
                {
                    nameof(Product.Name),
                    nameof(Product.Sku),
                    nameof(Product.Description),
                    nameof(Product.FragranceTagline),
                    nameof(Product.Price),
                    nameof(Product.StockQuantity),
                    nameof(Product.ReorderLevel),
                    nameof(Product.ImageUrl),
                    nameof(Product.IsActive),
                    nameof(Product.IsPublished),
                    nameof(Product.IsFeatured),
                    nameof(Product.IsBestSeller),
                    nameof(Product.CategoryId)
                };

                bool commercialChanged = commercialProps.Any(p => entry.Property(p).IsModified);
                if (!commercialChanged)
                {
                    // Only fragrance discovery fields or other non-commercial fields changed. Skip!
                    continue;
                }
            }

            string categoryName = string.Empty;
            if (product.Category != null)
            {
                categoryName = product.Category.Name;
            }
            else if (product.CategoryId > 0)
            {
                categoryName = context.Categories.Local.FirstOrDefault(c => c.Id == product.CategoryId)?.Name
                    ?? context.Categories.Where(c => c.Id == product.CategoryId).Select(c => c.Name).FirstOrDefault()
                    ?? string.Empty;
            }

            var dto = new ProductSyncDto
            {
                Sku = product.Sku,
                Name = product.Name,
                Description = product.Description,
                FragranceTagline = product.FragranceTagline,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                ReorderLevel = product.ReorderLevel,
                ImageUrl = product.ImageUrl,
                ImageSha256 = ComputeImageHash(product.ImageUrl),
                CategoryName = categoryName,
                IsActive = product.IsActive,
                IsPublished = product.IsPublished,
                IsFeatured = product.IsFeatured,
                IsBestSeller = product.IsBestSeller,
                Version = 1,
                ModifiedAtUtc = DateTime.UtcNow
            };

            var payload = JsonSerializer.Serialize(dto);
            events.Add(new SyncOutboxEvent
            {
                EntityType = "Product",
                EntitySyncKey = product.Sku,
                Operation = op,
                PayloadJson = payload,
                State = SyncEventState.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                EntityVersion = 1,
                ContentHash = ComputeStringHash(payload)
            });
        }

        var categoryEntries = changeTracker.Entries<Category>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in categoryEntries)
        {
            var cat = entry.Entity;
            string op = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(op)) continue;

            if (entry.State == EntityState.Modified)
            {
                var catProps = new[] { nameof(Category.Name), nameof(Category.Description), nameof(Category.ImageUrl) };
                if (!catProps.Any(p => entry.Property(p).IsModified))
                {
                    continue;
                }
            }

            var dto = new CategorySyncDto
            {
                Name = cat.Name,
                Description = cat.Description,
                ImageUrl = cat.ImageUrl,
                ImageSha256 = ComputeImageHash(cat.ImageUrl),
                Version = 1,
                ModifiedAtUtc = DateTime.UtcNow
            };

            var payload = JsonSerializer.Serialize(dto);
            events.Add(new SyncOutboxEvent
            {
                EntityType = "Category",
                EntitySyncKey = cat.Name,
                Operation = op,
                PayloadJson = payload,
                State = SyncEventState.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                EntityVersion = 1,
                ContentHash = ComputeStringHash(payload)
            });
        }

        return events;
    }

    public static string? ComputeImageHash(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Keep Cloudinary / remote URLs directly
        }

        try
        {
            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
            if (File.Exists(fullPath))
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(fullPath);
                var hashBytes = sha256.ComputeHash(stream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }
        catch
        {
            // Ignore hashing error gracefully
        }
        return null;
    }

    public static string ComputeStringHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
    }
}
