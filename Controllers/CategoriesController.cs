using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Services.Interfaces;
using Stockify.ViewModels.Categories;

namespace Stockify.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IStoreCacheService _storeCacheService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IMediaDeletionQueue _mediaDeletionQueue;
    private readonly ILogger<CategoriesController>? _logger;

    public CategoriesController(
        ApplicationDbContext context, 
        IStoreCacheService storeCacheService,
        IImageStorageService imageStorageService,
        IMediaDeletionQueue mediaDeletionQueue,
        ILogger<CategoriesController>? logger = null)
    {
        _context = context;
        _storeCacheService = storeCacheService;
        _imageStorageService = imageStorageService;
        _mediaDeletionQueue = mediaDeletionQueue;
        _logger = logger;
    }

    private void InvalidateStoreCache()
    {
        _storeCacheService.InvalidateAll();
    }

    private static bool ValidateUploadedImage(IFormFile file, out string? error)
    {
        return Stockify.Common.Security.ImageSecurityValidator.Validate(file, out error, allowSvg: false, maxSizeBytes: 5 * 1024 * 1024);
    }

    // GET: Categories
    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                ProductsCount = c.Products.Count,
                CreatedAt = c.CreatedAt
            })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    // GET: Categories/Create
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create()
    {
        return View(new CategoryViewModel());
    }

    // POST: Categories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CategoryViewModel model)
    {
        if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
        {
            ModelState.AddModelError("ImageFile", imageError!);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedName = model.Name.Trim();
        var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == normalizedName.ToLower());
        if (exists)
        {
            ModelState.AddModelError("Name", "A category with this name already exists.");
            return View(model);
        }

        string? uploadedUrl = null;
        if (model.ImageFile != null)
        {
            try
            {
                uploadedUrl = await _imageStorageService.UploadImageAsync(model.ImageFile, "roven/categories");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload category image");
                ModelState.AddModelError("ImageFile", "فشل حفظ أو رفع صورة القسم. يرجى التأكد من صلاحية الملف والمحاولة مرة أخرى.");
                return View(model);
            }
        }

        var category = new Category
        {
            Name = normalizedName,
            Description = model.Description?.Trim(),
            ImageUrl = uploadedUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        TempData["SuccessMessage"] = $"Category '{category.Name}' created successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Categories/Edit/5
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var model = new CategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            CreatedAt = category.CreatedAt
        };

        return View(model);
    }

    // POST: Categories/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id, CategoryViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
        {
            ModelState.AddModelError("ImageFile", imageError!);
        }

        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var normalizedName = model.Name.Trim();
        var nameConflict = await _context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == normalizedName.ToLower());

        if (nameConflict)
        {
            ModelState.AddModelError("Name", "Another category already exists with this name.");
        }

        if (!ModelState.IsValid)
        {
            model.ImageUrl = category.ImageUrl;
            return View(model);
        }

        string? oldImageUrl = category.ImageUrl;
        string? newlyUploadedUrl = null;

        if (model.ImageFile != null)
        {
            try
            {
                newlyUploadedUrl = await _imageStorageService.UploadImageAsync(model.ImageFile, "roven/categories");
                category.ImageUrl = newlyUploadedUrl;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload category image replacement");
                ModelState.AddModelError("ImageFile", "فشل رفع الصورة البديلة للقسم.");
                model.ImageUrl = category.ImageUrl;
                return View(model);
            }
        }
        else if (model.RemoveImage)
        {
            category.ImageUrl = null;
            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                try
                {
                    await _imageStorageService.DeleteImageAsync(oldImageUrl);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete old image {Url}", oldImageUrl);
                }
            }
        }

        category.Name = normalizedName;
        category.Description = model.Description?.Trim();

        _context.Categories.Update(category);
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        if (!string.IsNullOrWhiteSpace(newlyUploadedUrl) &&
            !string.IsNullOrWhiteSpace(oldImageUrl) &&
            !string.Equals(oldImageUrl, newlyUploadedUrl, StringComparison.OrdinalIgnoreCase) &&
            oldImageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            await _mediaDeletionQueue.EnqueueAsync(oldImageUrl);
        }

        TempData["SuccessMessage"] = $"Category '{category.Name}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Categories/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    // POST: Categories/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            TempData["ErrorMessage"] = "Category not found.";
            return RedirectToAction(nameof(Index));
        }

        // Business Rule 5.2: Prevent deletion if products are linked
        var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
        {
            TempData["ErrorMessage"] = $"Cannot delete category '{category.Name}' because it has active products assigned to it. Reassign or delete those products first.";
            return RedirectToAction(nameof(Index));
        }

        var catImg = category.ImageUrl;
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        InvalidateStoreCache();

        if (!string.IsNullOrWhiteSpace(catImg))
        {
            if (catImg.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
            {
                await _mediaDeletionQueue.EnqueueAsync(catImg);
            }
            else
            {
                try { await _imageStorageService.DeleteImageAsync(catImg); } catch { }
            }
        }

        TempData["SuccessMessage"] = $"Category '{category.Name}' deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
}
