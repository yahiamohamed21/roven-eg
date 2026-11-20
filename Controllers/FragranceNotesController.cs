using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.FragranceNotes;

namespace Stockify.Controllers;

[Authorize(Roles = Roles.Admin)]
public class FragranceNotesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FragranceNotesController>? _logger;

    public FragranceNotesController(ApplicationDbContext context, ILogger<FragranceNotesController>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    // GET: FragranceNotes
    public async Task<IActionResult> Index(int? category, string? search, bool? activeOnly)
    {
        var query = _context.FragranceNotes
            .Include(fn => fn.ProductFragranceNotes)
            .Include(fn => fn.QuickPicks)
            .AsNoTracking();

        if (category.HasValue && category.Value > 0)
        {
            query = query.Where(fn => (int)fn.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(fn => fn.Name.ToLower().Contains(s) || fn.ArabicName.Contains(s));
        }

        if (activeOnly.HasValue && activeOnly.Value)
        {
            query = query.Where(fn => fn.IsActive);
        }

        var notesList = await query
            .OrderBy(fn => fn.Category)
            .ThenBy(fn => fn.DisplayOrder)
            .ThenBy(fn => fn.Name)
            .Select(fn => new FragranceNoteListItemViewModel
            {
                Id = fn.Id,
                Name = fn.Name,
                ArabicName = fn.ArabicName,
                Category = fn.Category,
                CategoryNameEn = GetCategoryNameEn(fn.Category),
                CategoryNameAr = GetCategoryNameAr(fn.Category),
                IsActive = fn.IsActive,
                DisplayOrder = fn.DisplayOrder,
                IsLoveQuickPick = fn.QuickPicks.Any(qp => qp.Context == QuickPickContext.Love),
                IsAvoidQuickPick = fn.QuickPicks.Any(qp => qp.Context == QuickPickContext.Avoid),
                ProductsCount = fn.ProductFragranceNotes.Count,
                CreatedAt = fn.CreatedAt
            })
            .ToListAsync();

        var totalCount = await _context.FragranceNotes.CountAsync();
        var activeCount = await _context.FragranceNotes.CountAsync(fn => fn.IsActive);
        var loveQuickCount = await _context.FragranceNoteQuickPicks.CountAsync(qp => qp.Context == QuickPickContext.Love);
        var avoidQuickCount = await _context.FragranceNoteQuickPicks.CountAsync(qp => qp.Context == QuickPickContext.Avoid);

        var viewModel = new FragranceNoteIndexViewModel
        {
            Notes = notesList,
            SelectedCategory = category,
            SearchTerm = search,
            ActiveOnly = activeOnly,
            TotalNotesCount = totalCount,
            ActiveNotesCount = activeCount,
            LoveQuickPicksCount = loveQuickCount,
            AvoidQuickPicksCount = avoidQuickCount
        };

        return View(viewModel);
    }

    // GET: FragranceNotes/Create
    public IActionResult Create()
    {
        var model = new FragranceNoteFormViewModel
        {
            IsActive = true,
            DisplayOrder = 10
        };
        return View(model);
    }

    // POST: FragranceNotes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FragranceNoteFormViewModel model)
    {
        var trimmedName = model.Name.Trim();
        var trimmedArabicName = model.ArabicName.Trim();

        // Check for duplicate English name
        if (await _context.FragranceNotes.AnyAsync(n => n.Name.ToLower() == trimmedName.ToLower()))
        {
            ModelState.AddModelError("Name", $"A fragrance note with the name '{trimmedName}' already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var note = new FragranceNote
        {
            Name = trimmedName,
            ArabicName = trimmedArabicName,
            Category = model.Category,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.FragranceNotes.Add(note);
        await _context.SaveChangesAsync();

        // Add Quick Picks if selected
        if (model.IsLoveQuickPick)
        {
            _context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
            {
                FragranceNoteId = note.Id,
                Context = QuickPickContext.Love,
                DisplayOrder = model.DisplayOrder
            });
        }

        if (model.IsAvoidQuickPick)
        {
            _context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
            {
                FragranceNoteId = note.Id,
                Context = QuickPickContext.Avoid,
                DisplayOrder = model.DisplayOrder
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Fragrance note '{note.Name} ({note.ArabicName})' was successfully created!";
        return RedirectToAction(nameof(Index));
    }

    // GET: FragranceNotes/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var note = await _context.FragranceNotes
            .Include(n => n.QuickPicks)
            .Include(n => n.ProductFragranceNotes)
                .ThenInclude(pfn => pfn.Product)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null)
        {
            return NotFound();
        }

        var model = new FragranceNoteFormViewModel
        {
            Id = note.Id,
            Name = note.Name,
            ArabicName = note.ArabicName,
            Category = note.Category,
            DisplayOrder = note.DisplayOrder,
            IsActive = note.IsActive,
            IsLoveQuickPick = note.QuickPicks.Any(qp => qp.Context == QuickPickContext.Love),
            IsAvoidQuickPick = note.QuickPicks.Any(qp => qp.Context == QuickPickContext.Avoid),
            AssignedProductsCount = note.ProductFragranceNotes.Count,
            AssignedProductNames = note.ProductFragranceNotes
                .Where(pfn => pfn.Product != null)
                .Select(pfn => pfn.Product.Name)
                .Distinct()
                .ToList()
        };

        return View(model);
    }

    // POST: FragranceNotes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FragranceNoteFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var note = await _context.FragranceNotes
            .Include(n => n.QuickPicks)
            .Include(n => n.ProductFragranceNotes)
                .ThenInclude(pfn => pfn.Product)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null)
        {
            return NotFound();
        }

        var trimmedName = model.Name.Trim();
        var trimmedArabicName = model.ArabicName.Trim();

        // Check for duplicate English name excluding current
        if (await _context.FragranceNotes.AnyAsync(n => n.Id != id && n.Name.ToLower() == trimmedName.ToLower()))
        {
            ModelState.AddModelError("Name", $"Another fragrance note already uses the name '{trimmedName}'.");
        }

        if (!ModelState.IsValid)
        {
            model.AssignedProductsCount = note.ProductFragranceNotes.Count;
            model.AssignedProductNames = note.ProductFragranceNotes
                .Where(pfn => pfn.Product != null)
                .Select(pfn => pfn.Product.Name)
                .Distinct()
                .ToList();
            return View(model);
        }

        note.Name = trimmedName;
        note.ArabicName = trimmedArabicName;
        note.Category = model.Category;
        note.DisplayOrder = model.DisplayOrder;
        note.IsActive = model.IsActive;

        // Synchronize Love Quick Pick
        var existingLove = note.QuickPicks.FirstOrDefault(qp => qp.Context == QuickPickContext.Love);
        if (model.IsLoveQuickPick && existingLove == null)
        {
            _context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
            {
                FragranceNoteId = note.Id,
                Context = QuickPickContext.Love,
                DisplayOrder = model.DisplayOrder
            });
        }
        else if (!model.IsLoveQuickPick && existingLove != null)
        {
            _context.FragranceNoteQuickPicks.Remove(existingLove);
        }

        // Synchronize Avoid Quick Pick
        var existingAvoid = note.QuickPicks.FirstOrDefault(qp => qp.Context == QuickPickContext.Avoid);
        if (model.IsAvoidQuickPick && existingAvoid == null)
        {
            _context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
            {
                FragranceNoteId = note.Id,
                Context = QuickPickContext.Avoid,
                DisplayOrder = model.DisplayOrder
            });
        }
        else if (!model.IsAvoidQuickPick && existingAvoid != null)
        {
            _context.FragranceNoteQuickPicks.Remove(existingAvoid);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Fragrance note '{note.Name}' was successfully updated!";
        return RedirectToAction(nameof(Index));
    }

    // POST: FragranceNotes/ToggleActive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var note = await _context.FragranceNotes.FindAsync(id);
        if (note == null)
        {
            return NotFound();
        }

        note.IsActive = !note.IsActive;
        await _context.SaveChangesAsync();

        var status = note.IsActive ? "activated" : "deactivated";
        TempData["SuccessMessage"] = $"Note '{note.Name}' was {status} successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: FragranceNotes/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _context.FragranceNotes
            .Include(n => n.ProductFragranceNotes)
                .ThenInclude(pfn => pfn.Product)
            .Include(n => n.QuickPicks)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null)
        {
            return NotFound();
        }

        // Prevent deletion if note is assigned to any product!
        if (note.ProductFragranceNotes.Any())
        {
            var productNames = string.Join(", ", note.ProductFragranceNotes
                .Where(pfn => pfn.Product != null)
                .Select(pfn => pfn.Product.Name)
                .Distinct());

            TempData["ErrorMessage"] = $"Cannot delete fragrance note '{note.Name}' because it is assigned to {note.ProductFragranceNotes.Count} product(s): {productNames}. You can deactivate the note instead to hide it from future selections.";
            return RedirectToAction(nameof(Index));
        }

        // Remove any associated Quick Picks
        if (note.QuickPicks.Any())
        {
            _context.FragranceNoteQuickPicks.RemoveRange(note.QuickPicks);
        }

        _context.FragranceNotes.Remove(note);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Fragrance note '{note.Name}' was deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: FragranceNotes/QuickPicks?context=1
    public async Task<IActionResult> QuickPicks(QuickPickContext context = QuickPickContext.Love)
    {
        var quickPicks = await _context.FragranceNoteQuickPicks
            .Include(qp => qp.FragranceNote)
            .Where(qp => qp.Context == context)
            .OrderBy(qp => qp.DisplayOrder)
            .ThenBy(qp => qp.FragranceNote.Name)
            .Select(qp => new QuickPickItemViewModel
            {
                FragranceNoteId = qp.FragranceNoteId,
                Name = qp.FragranceNote.Name,
                ArabicName = qp.FragranceNote.ArabicName,
                Category = qp.FragranceNote.Category,
                DisplayOrder = qp.DisplayOrder
            })
            .ToListAsync();

        var existingNoteIds = quickPicks.Select(qp => qp.FragranceNoteId).ToHashSet();

        var availableNotes = await _context.FragranceNotes
            .Where(n => n.IsActive && !existingNoteIds.Contains(n.Id))
            .OrderBy(n => n.Name)
            .Select(n => new SelectListItem
            {
                Value = n.Id.ToString(),
                Text = $"{n.Name} ({n.ArabicName}) - {GetCategoryNameEn(n.Category)}"
            })
            .ToListAsync();

        var model = new QuickPicksManagementViewModel
        {
            Context = context,
            ContextTitle = context == QuickPickContext.Love ? "Quiz Question 2: Loved Notes (النوتات المحبوبة)" : "Quiz Question 3: Avoided Notes (النوتات غير المرغوبة)",
            ContextDescription = context == QuickPickContext.Love
                ? "Manage the prominent quick-pick pills displayed at the top of Step 2 (Loved Notes) during customer fragrance discovery."
                : "Manage the prominent quick-pick pills displayed at the top of Step 3 (Avoided Notes) to help customers quickly filter out common dislikes.",
            QuickPicks = quickPicks,
            AvailableNotesSelectList = availableNotes
        };

        return View(model);
    }

    // POST: FragranceNotes/AddQuickPick
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuickPick(QuickPickContext context, int noteId)
    {
        if (noteId <= 0)
        {
            TempData["ErrorMessage"] = "Please select a valid note to add to Quick Picks.";
            return RedirectToAction(nameof(QuickPicks), new { context });
        }

        var exists = await _context.FragranceNoteQuickPicks
            .AnyAsync(qp => qp.FragranceNoteId == noteId && qp.Context == context);

        if (!exists)
        {
            var maxOrder = await _context.FragranceNoteQuickPicks
                .Where(qp => qp.Context == context)
                .Select(qp => (int?)qp.DisplayOrder)
                .MaxAsync() ?? 0;

            _context.FragranceNoteQuickPicks.Add(new FragranceNoteQuickPick
            {
                FragranceNoteId = noteId,
                Context = context,
                DisplayOrder = maxOrder + 1
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Note added to Quick Picks successfully!";
        }

        return RedirectToAction(nameof(QuickPicks), new { context });
    }

    // POST: FragranceNotes/RemoveQuickPick
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveQuickPick(QuickPickContext context, int noteId)
    {
        var qp = await _context.FragranceNoteQuickPicks
            .FirstOrDefaultAsync(q => q.FragranceNoteId == noteId && q.Context == context);

        if (qp != null)
        {
            _context.FragranceNoteQuickPicks.Remove(qp);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Note removed from Quick Picks.";
        }

        return RedirectToAction(nameof(QuickPicks), new { context });
    }

    // POST: FragranceNotes/UpdateQuickPickOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuickPickOrder(QuickPickContext context, int noteId, int displayOrder)
    {
        var qp = await _context.FragranceNoteQuickPicks
            .FirstOrDefaultAsync(q => q.FragranceNoteId == noteId && q.Context == context);

        if (qp != null)
        {
            qp.DisplayOrder = displayOrder;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Quick pick order updated.";
        }

        return RedirectToAction(nameof(QuickPicks), new { context });
    }

    private static string GetCategoryNameEn(FragranceNoteCategory category) => category switch
    {
        FragranceNoteCategory.Citrus => "Citrus",
        FragranceNoteCategory.Floral => "Floral",
        FragranceNoteCategory.Woody => "Woody",
        FragranceNoteCategory.OrientalAmber => "Oriental / Amber",
        FragranceNoteCategory.FreshAromatic => "Fresh / Aromatic",
        FragranceNoteCategory.GourmandSweet => "Gourmand / Sweet",
        _ => "General"
    };

    private static string GetCategoryNameAr(FragranceNoteCategory category) => category switch
    {
        FragranceNoteCategory.Citrus => "حمضيات",
        FragranceNoteCategory.Floral => "زهور",
        FragranceNoteCategory.Woody => "أخشاب",
        FragranceNoteCategory.OrientalAmber => "شرقي وعنبر",
        FragranceNoteCategory.FreshAromatic => "منعش وأروماتيك",
        FragranceNoteCategory.GourmandSweet => "غورماند وحلويات",
        _ => "عام"
    };
}
