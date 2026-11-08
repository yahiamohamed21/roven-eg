using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Inventory;

namespace Stockify.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Inventory
    public async Task<IActionResult> Index(string? search, string? status, int? categoryId)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.InventoryTransactions)
            .Where(p => p.IsActive)
            .AsNoTracking();

        // 1. Calculate KPI Statistics across all active inventory (Single Combined Query)
        var invStats = await _context.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalValue = g.Sum(p => p.Price * p.StockQuantity),
                TotalUnits = g.Sum(p => p.StockQuantity),
                LowStock = g.Count(p => p.StockQuantity <= p.ReorderLevel)
            })
            .FirstOrDefaultAsync();

        ViewBag.TotalInventoryValue = invStats?.TotalValue ?? 0m;
        ViewBag.TotalUnitsInStock = invStats?.TotalUnits ?? 0;
        ViewBag.LowStockCount = invStats?.LowStock ?? 0;

        ViewBag.LowStockProducts = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .Take(10)
            .Select(p => new Stockify.ViewModels.Dashboard.LowStockDashboardViewModel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel
            })
            .ToListAsync();

        var todayUtc = DateTime.UtcNow.Date;
        ViewBag.TodayMovementsCount = await _context.InventoryTransactions
            .CountAsync(t => t.TransactionDate >= todayUtc);

        // 2. Populate Categories Dropdown
        ViewBag.Categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = categoryId == c.Id
            })
            .ToListAsync();

        ViewBag.CurrentSearch = search;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentCategory = categoryId;

        // 3. Apply Filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.Sku.Contains(s));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.ToLowerInvariant() switch
            {
                "sufficient" => query.Where(p => p.StockQuantity > p.ReorderLevel),
                "lowstock" => query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel),
                "outofstock" => query.Where(p => p.StockQuantity <= 0),
                _ => query
            };
        }

        var inventoryItems = await query
            .OrderBy(p => p.Name)
            .Select(p => new InventoryItemViewModel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel,
                UnitPrice = p.Price,
                LastMovementDate = p.InventoryTransactions
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => (DateTime?)t.TransactionDate)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return View(inventoryItems);
    }

    // GET: Inventory/StockIn
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> StockIn(int? productId)
    {
        var model = new StockInViewModel
        {
            ProductId = productId ?? 0,
            ReceivedDate = DateTime.UtcNow,
            ProductsList = await GetProductsSelectListAsync(productId)
        };

        return View(model);
    }

    // POST: Inventory/StockIn
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> StockIn(StockInViewModel model)
    {
        var product = await _context.Products.FindAsync(model.ProductId);
        if (product == null)
        {
            ModelState.AddModelError(nameof(model.ProductId), "Selected product was not found in catalog.");
        }

        if (!ModelState.IsValid)
        {
            model.ProductsList = await GetProductsSelectListAsync(model.ProductId);
            return View(model);
        }

        // Apply Stock Addition
        product!.StockQuantity += model.Quantity;
        _context.Products.Update(product);

        // Record Audit Trail Transaction
        var currentUserId = _userManager.GetUserId(User);
        var noteText = string.IsNullOrWhiteSpace(model.SupplierReference)
            ? model.Notes
            : $"Ref/Supplier: {model.SupplierReference}. {model.Notes}".Trim();

        var transaction = new InventoryTransaction
        {
            ProductId = product.Id,
            Quantity = model.Quantity,
            TransactionType = TransactionType.StockIn,
            TransactionDate = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(noteText) ? "Inbound purchase / stock replenishment" : noteText,
            UserId = currentUserId
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Stock In recorded successfully! Received +{model.Quantity} units for '{product.Name}'. Current stock is now {product.StockQuantity} units.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Inventory/Adjust
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Adjust(int? productId)
    {
        var model = new StockAdjustmentViewModel
        {
            ProductId = productId ?? 0,
            AdjustmentType = TransactionType.Adjustment,
            ProductsList = await GetProductsSelectListAsync(productId)
        };

        return View(model);
    }

    // POST: Inventory/Adjust
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Adjust(StockAdjustmentViewModel model)
    {
        var product = await _context.Products.FindAsync(model.ProductId);
        if (product == null)
        {
            ModelState.AddModelError(nameof(model.ProductId), "Selected product was not found in catalog.");
        }
        else
        {
            // Business Rule Protection: Prevent negative stock on deduction
            if ((model.AdjustmentType == TransactionType.StockOut || model.AdjustmentType == TransactionType.Adjustment)
                && product.StockQuantity < model.Quantity)
            {
                ModelState.AddModelError(nameof(model.Quantity),
                    $"Cannot deduct {model.Quantity} units. Only {product.StockQuantity} units are currently available in inventory.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.ProductsList = await GetProductsSelectListAsync(model.ProductId);
            return View(model);
        }

        // Apply adjustment
        int signedChange = model.Quantity;
        if (model.AdjustmentType == TransactionType.StockOut || model.AdjustmentType == TransactionType.Adjustment)
        {
            product!.StockQuantity -= model.Quantity;
            signedChange = -model.Quantity;
        }
        else if (model.AdjustmentType == TransactionType.Return)
        {
            product!.StockQuantity += model.Quantity;
            signedChange = model.Quantity;
        }

        _context.Products.Update(product!);

        var currentUserId = _userManager.GetUserId(User);
        var transaction = new InventoryTransaction
        {
            ProductId = product!.Id,
            Quantity = model.Quantity,
            TransactionType = model.AdjustmentType,
            TransactionDate = DateTime.UtcNow,
            Notes = model.Reason.Trim(),
            UserId = currentUserId
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Inventory adjustment recorded successfully for '{product.Name}'. New stock balance: {product.StockQuantity} units.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Inventory/LowStock
    public async Task<IActionResult> LowStock()
    {
        var lowStockItems = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .Select(p => new LowStockItemViewModel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel
            })
            .ToListAsync();

        return View(lowStockItems);
    }

    // GET: Inventory/History
    public async Task<IActionResult> History(int? productId, TransactionType? type, string? search)
    {
        var query = _context.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .AsNoTracking();

        if (productId.HasValue && productId.Value > 0)
        {
            query = query.Where(t => t.ProductId == productId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(t => t.TransactionType == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => t.Product != null && (t.Product.Name.Contains(s) || t.Product.Sku.Contains(s) || (t.Notes != null && t.Notes.Contains(s))));
        }

        ViewBag.ProductId = productId;
        ViewBag.SelectedType = type;
        ViewBag.SearchTerm = search;
        ViewBag.Products = await _context.Products
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} ({p.Sku})"
            })
            .ToListAsync();

        var history = await query
            .OrderByDescending(t => t.TransactionDate)
            .Take(200)
            .Select(t => new InventoryTransactionViewModel
            {
                Id = t.Id,
                ProductId = t.ProductId,
                ProductName = t.Product != null ? t.Product.Name : "Unknown Product",
                Sku = t.Product != null ? t.Product.Sku : "—",
                Quantity = t.Quantity,
                TransactionType = t.TransactionType,
                TransactionDate = t.TransactionDate,
                Notes = t.Notes,
                PerformerName = t.User != null ? (t.User.FullName ?? t.User.UserName ?? "User") : "Sales Checkout (System)"
            })
            .ToListAsync();

        return View(history);
    }

    #region Helper Methods

    private async Task<List<SelectListItem>> GetProductsSelectListAsync(int? selectedProductId = null)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} (SKU: {p.Sku}) — Current Stock: {p.StockQuantity} units",
                Selected = selectedProductId.HasValue && p.Id == selectedProductId.Value
            })
            .ToListAsync();
    }

    #endregion
}
