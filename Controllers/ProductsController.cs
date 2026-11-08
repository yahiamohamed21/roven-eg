using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Products;

namespace Stockify.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: Products
    public async Task<IActionResult> Index(int? categoryId, string? status, string? search)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsNoTracking();

        // Summary Statistics (calculated across all active products)
        ViewBag.TotalProducts = await query.CountAsync();
        ViewBag.InStockCount = await query.CountAsync(p => p.StockQuantity > p.ReorderLevel);
        ViewBag.LowStockCount = await query.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel);
        ViewBag.OutOfStockCount = await query.CountAsync(p => p.StockQuantity <= 0);

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
            imageUrl = await SaveUploadedImageAsync(model.ImageFile);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Sku = normalizedSku,
            Description = model.Description?.Trim(),
            Price = model.Price,
            StockQuantity = model.StockQuantity,
            ReorderLevel = model.ReorderLevel,
            CategoryId = model.CategoryId,
            ImageUrl = imageUrl,
            IsActive = true,
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
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ReorderLevel = product.ReorderLevel,
            CategoryId = product.CategoryId,
            IsActive = product.IsActive,
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

        if (model.ImageFile != null)
        {
            product.ImageUrl = await SaveUploadedImageAsync(model.ImageFile);
        }

        product.Name = model.Name.Trim();
        product.Sku = normalizedSku;
        product.Description = model.Description?.Trim();
        product.Price = model.Price;
        product.ReorderLevel = model.ReorderLevel;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

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

            TempData["WarningMessage"] = $"Product '{product.Name}' has existing sales order history and cannot be permanently deleted. It has been archived (IsActive = false).";
            return RedirectToAction(nameof(Index));
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

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
        error = null;
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(ext))
        {
            error = "Only JPG, PNG, and WebP image formats are permitted.";
            return false;
        }

        const long maxBytes = 3 * 1024 * 1024; // 3MB limit
        if (file.Length > maxBytes)
        {
            error = "Image file size cannot exceed 3MB.";
            return false;
        }

        return true;
    }

    private async Task<string> SaveUploadedImageAsync(IFormFile file)
    {
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return $"/uploads/products/{uniqueFileName}";
    }

    #endregion
}
