using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.ViewModels.Categories;

namespace Stockify.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
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

        var category = new Category
        {
            Name = normalizedName,
            Description = model.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

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

        if (!ModelState.IsValid)
        {
            return View(model);
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
            return View(model);
        }

        category.Name = normalizedName;
        category.Description = model.Description?.Trim();

        _context.Categories.Update(category);
        await _context.SaveChangesAsync();

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

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Category '{category.Name}' deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
}
