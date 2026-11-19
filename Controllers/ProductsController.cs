using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;
using Stockify.ViewModels.Products;

namespace Stockify.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly Stockify.Services.Interfaces.IImageStorageService _imageStorageService;
    private readonly IStoreCacheService _storeCacheService;
    private readonly IMediaDeletionQueue _mediaDeletionQueue;
    private readonly ILogger<ProductsController>? _logger;

    public ProductsController(
        ApplicationDbContext context, 
        IWebHostEnvironment environment,
        Stockify.Services.Interfaces.IImageStorageService imageStorageService,
        IStoreCacheService storeCacheService,
        IMediaDeletionQueue mediaDeletionQueue,
        ILogger<ProductsController>? logger = null)
    {
        _context = context;
        _environment = environment;
        _imageStorageService = imageStorageService;
        _storeCacheService = storeCacheService;
        _mediaDeletionQueue = mediaDeletionQueue;
        _logger = logger;
    }

    private void InvalidateStoreCache()
    {
        _storeCacheService.InvalidateAll();
    }

    // GET: Products
    public async Task<IActionResult> Index(int? categoryId, string? status, string? search)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsNoTracking();

        // Summary Statistics (calculated across all active products in 1 single combined query)
        var productKpis = await _context.Products.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                InStock = g.Count(p => p.StockQuantity > p.ReorderLevel),
                LowStock = g.Count(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel),
                OutOfStock = g.Count(p => p.StockQuantity <= 0)
            })
            .FirstOrDefaultAsync();

        ViewBag.TotalProducts = productKpis?.Total ?? 0;
        ViewBag.InStockCount = productKpis?.InStock ?? 0;
        ViewBag.LowStockCount = productKpis?.LowStock ?? 0;
        ViewBag.OutOfStockCount = productKpis?.OutOfStock ?? 0;

        // Populate Categories Filter Dropdown
        ViewBag.Categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = categoryId == c.Id })
            .ToListAsync();

        // Apply filters
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.ToLower() switch
            {
                "instock" => query.Where(p => p.StockQuantity > p.ReorderLevel),
                "lowstock" => query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel),
                "outofstock" => query.Where(p => p.StockQuantity <= 0),
                "alert" or "low" => query.Where(p => p.StockQuantity <= p.ReorderLevel),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s));
        }

        var products = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ReorderLevel = p.ReorderLevel,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return View(products);
    }

    // GET: Products/Create
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create()
    {
        var model = new ProductCreateViewModel
        {
            CategoriesList = await GetCategoriesSelectListAsync()
        };
        return View(model);
    }

    // POST: Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(ProductCreateViewModel model)
    {
        // Validate unique SKU
        var normalizedSku = model.Sku.Trim().ToUpperInvariant();
        if (await _context.Products.AnyAsync(p => p.Sku.ToUpper() == normalizedSku))
        {
            ModelState.AddModelError("Sku", "SKU must be unique. Another product already uses this code.");
        }

        // Validate image file if uploaded
        if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
        {
            ModelState.AddModelError("ImageFile", imageError!);
        }

        if (!ModelState.IsValid)
        {
            model.CategoriesList = await GetCategoriesSelectListAsync();
            return View(model);
        }

        string? imageUrl = null;
        if (model.ImageFile != null)
        {
            try
            {
                imageUrl = await SaveUploadedImageAsync(model.ImageFile);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload image for new product: {Name}", model.Name);
                ModelState.AddModelError("ImageFile", "فشل حفظ أو رفع صورة المنتج. يرجى التأكد من صلاحية الملف والمحاولة مرة أخرى.");
                model.CategoriesList = await GetCategoriesSelectListAsync();
                return View(model);
            }
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Sku = normalizedSku,
            Description = model.Description?.Trim(),
            FragranceTagline = string.IsNullOrWhiteSpace(model.FragranceTagline) ? null : model.FragranceTagline.Trim(),
            Price = model.Price,
            StockQuantity = model.StockQuantity,
            ReorderLevel = model.ReorderLevel,
            CategoryId = model.CategoryId,
            ImageUrl = imageUrl,
            IsActive = true,
            IsFeatured = model.IsFeatured,
            IsBestSeller = model.IsBestSeller,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // If initial stock was provided, log an initial inventory transaction
        if (product.StockQuantity > 0)
        {
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = product.Id,
                Quantity = product.StockQuantity,
                TransactionType = TransactionType.StockIn,
                TransactionDate = DateTime.UtcNow,
                Notes = "Initial inventory stock entered at product creation"
            });
            await _context.SaveChangesAsync();
        }

        InvalidateStoreCache();

        TempData["SuccessMessage"] = $"Product '{product.Name}' added successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Products/Edit/5
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var model = new ProductEditViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            Description = product.Description,
            FragranceTagline = product.FragranceTagline,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ReorderLevel = product.ReorderLevel,
            CategoryId = product.CategoryId,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            IsBestSeller = product.IsBestSeller,
            ExistingImageUrl = product.ImageUrl,
            CategoriesList = await GetCategoriesSelectListAsync()
        };

        return View(model);
    }

    // POST: Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id, ProductEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var normalizedSku = model.Sku.Trim().ToUpperInvariant();
        if (await _context.Products.AnyAsync(p => p.Id != id && p.Sku.ToUpper() == normalizedSku))
        {
            ModelState.AddModelError("Sku", "Another product already uses this SKU code.");
        }

        if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
        {
            ModelState.AddModelError("ImageFile", imageError!);
        }

        if (!ModelState.IsValid)
        {
            model.ExistingImageUrl = product.ImageUrl;
            model.CategoriesList = await GetCategoriesSelectListAsync();
            return View(model);
        }

        string? oldImageUrl = product.ImageUrl;
        string? newlyUploadedUrl = null;

        if (model.ImageFile != null)
        {
            try
            {
                newlyUploadedUrl = await SaveUploadedImageAsync(model.ImageFile);
                product.ImageUrl = newlyUploadedUrl;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload replacement image for product {Id}", id);
                ModelState.AddModelError("ImageFile", "فشل حفظ أو رفع صورة المنتج البديلة. يرجى التأكد من صلاحية الملف والمحاولة مرة أخرى.");
                model.ExistingImageUrl = product.ImageUrl;
                model.CategoriesList = await GetCategoriesSelectListAsync();
                return View(model);
            }
        }
        else if (model.RemoveImage)
        {
            product.ImageUrl = null;
            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                try
                {
                    await _imageStorageService.DeleteImageAsync(oldImageUrl);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete old image {Url} from storage for product {Id}", oldImageUrl, id);
                }
            }
        }

        if (model.StockQuantity != product.StockQuantity)
        {
            var diff = model.StockQuantity - product.StockQuantity;
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userExists = !string.IsNullOrEmpty(currentUserId) && await _context.Users.AnyAsync(u => u.Id == currentUserId);

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = product.Id,
                Quantity = Math.Abs(diff),
                TransactionType = diff > 0 ? TransactionType.StockIn : TransactionType.Adjustment,
                TransactionDate = DateTime.UtcNow,
                Notes = $"تعديل مباشر للمخزون من صفحة تعديل المنتج (من {product.StockQuantity} إلى {model.StockQuantity})",
                UserId = userExists ? currentUserId : null
            });

            product.StockQuantity = model.StockQuantity;
        }

        product.Name = model.Name.Trim();
        product.Sku = normalizedSku;
        product.Description = model.Description?.Trim();
        product.FragranceTagline = string.IsNullOrWhiteSpace(model.FragranceTagline) ? null : model.FragranceTagline.Trim();
        product.Price = model.Price;
        product.ReorderLevel = model.ReorderLevel;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;
        product.IsFeatured = model.IsFeatured;
        product.IsBestSeller = model.IsBestSeller;

        try
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Database update failed after image upload for product {Id}. Preserving existing image reference.", id);
            if (!string.IsNullOrWhiteSpace(newlyUploadedUrl))
            {
                _logger?.LogWarning("Orphaned Cloudinary asset identified: {Url}", newlyUploadedUrl);
            }
            product.ImageUrl = oldImageUrl;
            ModelState.AddModelError("", "حدث خطأ أثناء حفظ بيانات العطر في قاعدة البيانات.");
            model.ExistingImageUrl = oldImageUrl;
            model.CategoriesList = await GetCategoriesSelectListAsync();
            return View(model);
        }

        InvalidateStoreCache();

        // Safe cleanup: Enqueue old Cloudinary image to background deletion queue
        if (!string.IsNullOrWhiteSpace(newlyUploadedUrl) &&
            !string.IsNullOrWhiteSpace(oldImageUrl) &&
            !string.Equals(oldImageUrl, newlyUploadedUrl, StringComparison.OrdinalIgnoreCase) &&
            oldImageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(oldImageUrl);
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Products/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.InventoryTransactions.OrderByDescending(t => t.TransactionDate).Take(10))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // POST: Products/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Index));
        }

        // Business Rule 6: If product has order items, apply Soft Delete (IsActive = false)
        var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
        if (hasOrders)
        {
            product.IsActive = false;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            InvalidateStoreCache();

            TempData["WarningMessage"] = $"Product '{product.Name}' has existing sales order history and cannot be permanently deleted. It has been archived (IsActive = false).";
            return RedirectToAction(nameof(Index));
        }

        var imageUrl = product.ImageUrl;
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        // Safe cleanup: Enqueue deleted product image to background deletion queue
        if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(imageUrl);
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' permanently deleted successfully!";
        return RedirectToAction(nameof(Index));
    }

    #region Helper Methods

    private async Task<List<SelectListItem>> GetCategoriesSelectListAsync()
    {
        return await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    private static bool ValidateUploadedImage(IFormFile file, out string? error)
    {
        return Stockify.Common.Security.ImageSecurityValidator.Validate(file, out error, allowSvg: false, maxSizeBytes: 5 * 1024 * 1024);
    }

    private async Task<string> SaveUploadedImageAsync(IFormFile file)
    {
        return await _imageStorageService.UploadImageAsync(file, "roven/products");
    }

    #endregion
}
