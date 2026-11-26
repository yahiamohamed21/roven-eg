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

    private void NormalizeAndValidatePricing(IProductPricingFormModel model)
    {
        var rawSizes = (model.Sizes ?? new List<ProductSizeInputViewModel>())
            .Where(s => s != null && (!string.IsNullOrWhiteSpace(s.SizeLabel) || s.CostPrice > 0 || s.ProfitAmount > 0))
            .ToList();

        if (rawSizes.Count == 0)
        {
            rawSizes.Add(new ProductSizeInputViewModel
            {
                SizeLabel = "100ml",
                CostPrice = Math.Max(0, model.CostPrice),
                ProfitAmount = Math.Max(0, model.ProfitAmount > 0 || model.CostPrice > 0 ? model.ProfitAmount : model.Price),
                IsDefault = true,
                DisplayOrder = 0
            });
        }

        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rawSizes.Count; i++)
        {
            var s = rawSizes[i];
            s.SizeLabel = (s.SizeLabel ?? string.Empty).Trim();
            s.DisplayOrder = i;

            if (string.IsNullOrWhiteSpace(s.SizeLabel))
            {
                ModelState.AddModelError("Price", $"يرجى إدخال اسم/مقاس الحجم في السطر رقم {i + 1} (مثال: 50ml أو 100ml).");
            }
            else if (!seenLabels.Add(s.SizeLabel))
            {
                ModelState.AddModelError("Price", $"الحجم '{s.SizeLabel}' مكرر أكثر من مرة لنفس المنتج. يرجى عدم تكرار الحجم.");
            }

            if (s.CostPrice < 0)
            {
                ModelState.AddModelError("Price", $"صافي المنتج للحجم '{s.SizeLabel}' لا يمكن أن يكون أقل من صفر.");
                s.CostPrice = 0;
            }
            if (s.ProfitAmount < 0)
            {
                ModelState.AddModelError("Price", $"المكسب للحجم '{s.SizeLabel}' لا يمكن أن يكون أقل من صفر.");
                s.ProfitAmount = 0;
            }

            s.SellingPrice = Math.Round(s.CostPrice + s.ProfitAmount, 2, MidpointRounding.AwayFromZero);
            if (s.SellingPrice < 0.01m)
            {
                ModelState.AddModelError("Price", $"إجمالي السعر للعميل للحجم '{(string.IsNullOrWhiteSpace(s.SizeLabel) ? $"#{i + 1}" : s.SizeLabel)}' (صافي + مكسب) يجب أن يكون أكبر من 0.");
            }
        }

        // Guarantee exactly one default size
        var defaultRow = rawSizes.FirstOrDefault(s => s.IsDefault) ?? rawSizes[0];
        foreach (var s in rawSizes)
        {
            s.IsDefault = ReferenceEquals(s, defaultRow);
        }

        model.Sizes = rawSizes;
        model.CostPrice = Math.Round(defaultRow.CostPrice, 2, MidpointRounding.AwayFromZero);
        model.ProfitAmount = Math.Round(defaultRow.ProfitAmount, 2, MidpointRounding.AwayFromZero);
        model.Price = defaultRow.SellingPrice;

        // Reconcile computed fields against any stale form-binding ModelState errors
        ModelState.Remove(nameof(IProductPricingFormModel.CostPrice));
        ModelState.Remove(nameof(IProductPricingFormModel.ProfitAmount));
        ModelState.Remove(nameof(IProductPricingFormModel.Price));
        for (int i = 0; i < rawSizes.Count; i++)
        {
            ModelState.Remove($"Sizes[{i}].SellingPrice");
            ModelState.Remove($"Sizes[{i}].IsDefault");
            ModelState.Remove($"Sizes[{i}].DisplayOrder");
        }
    }

    // GET: Products
    public async Task<IActionResult> Index(int? categoryId, string? status, string? search)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductSizes)
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
            query = query.Where(p => p.CategoryId == categoryId.Value || p.ProductCategories.Any(pc => pc.CategoryId == categoryId.Value));
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
                CostPrice = p.CostPrice,
                ProfitAmount = p.ProfitAmount,
                SizeLabels = p.ProductSizes
                    .OrderByDescending(s => s.IsDefault)
                    .ThenBy(s => s.DisplayOrder)
                    .Select(s => s.SizeLabel)
                    .ToList(),
                StockQuantity = p.StockQuantity,
                ReorderLevel = p.ReorderLevel,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CategoryNames = p.ProductCategories.Any() ? p.ProductCategories.Select(pc => pc.Category.Name).ToList() : new List<string> { p.Category != null ? p.Category.Name : "General" },
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
            Sizes = new List<ProductSizeInputViewModel>
            {
                new ProductSizeInputViewModel { SizeLabel = "100ml", IsDefault = true, DisplayOrder = 0 }
            },
            CategoriesList = await GetCategoriesSelectListAsync(),
            AvailableNotesGrouped = await GetGroupedFragranceNotesAsync()
        };
        return View(model);
    }

    // POST: Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(ProductCreateViewModel model)
    {
        try
        {
            // Null safety guards
            model.SelectedAudiences ??= new();
            model.SelectedSeasons ??= new();
            model.SelectedFragranceNoteIds ??= new();
            model.SelectedCategoryIds ??= new();

            if (model.CategoryId > 0 && !model.SelectedCategoryIds.Contains(model.CategoryId))
            {
                model.SelectedCategoryIds.Add(model.CategoryId);
            }
            else if (model.SelectedCategoryIds.Count > 0 && model.CategoryId <= 0)
            {
                model.CategoryId = model.SelectedCategoryIds.First();
            }

            if (model.SelectedCategoryIds.Count == 0 && model.CategoryId <= 0)
            {
                ModelState.AddModelError("SelectedCategoryIds", "يرجى تحديد قسم واحد على الأقل للمنتج (Please select at least one category).");
            }

            // Validate unique SKU
            var normalizedSku = (model.Sku ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedSku))
            {
                ModelState.AddModelError("Sku", "SKU code is required.");
            }
            else if (await _context.Products.AnyAsync(p => p.Sku.ToUpper() == normalizedSku))
            {
                ModelState.AddModelError("Sku", "SKU must be unique. Another product already uses this code.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Product name is required.");
            }

            // Validate image file if uploaded
            if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
            {
                ModelState.AddModelError("ImageFile", imageError!);
            }

            // Validate approval requirements if admin checked approval
            if (model.IsFragranceProfileApproved)
            {
                ValidateFragranceProfileCompleteness(model.SelectedAudiences, model.SelectedSeasons, model.Projection, model.SelectedFragranceNoteIds);
            }

            NormalizeAndValidatePricing(model);

            if (!ModelState.IsValid)
            {
                model.CategoriesList = await GetCategoriesSelectListAsync();
                model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
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
                    model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
                    return View(model);
                }
            }

            var product = new Product
            {
                Name = (model.Name ?? string.Empty).Trim(),
                Sku = normalizedSku,
                Description = model.Description?.Trim(),
                FragranceTagline = string.IsNullOrWhiteSpace(model.FragranceTagline) ? null : model.FragranceTagline.Trim(),
                CostPrice = model.CostPrice,
                ProfitAmount = model.ProfitAmount,
                Price = model.Price,
                StockQuantity = model.StockQuantity,
                ReorderLevel = model.ReorderLevel,
                CategoryId = model.CategoryId,
                ImageUrl = imageUrl,
                IsActive = true,
                IsFeatured = model.IsFeatured,
                IsBestSeller = model.IsBestSeller,
                Projection = model.Projection,
                InspiredBy = string.IsNullOrWhiteSpace(model.InspiredBy) ? null : model.InspiredBy.Trim(),
                IsFragranceProfileApproved = model.IsFragranceProfileApproved,
                FragranceProfileReviewedAt = model.IsFragranceProfileApproved ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };

            // Add Target Audiences
            foreach (var aud in model.SelectedAudiences.Distinct())
            {
                product.TargetAudiences.Add(new ProductTargetAudience { Product = product, Audience = aud });
            }

            // Add Seasons
            foreach (var season in model.SelectedSeasons.Distinct())
            {
                product.Seasons.Add(new ProductSeason { Product = product, Season = season });
            }

            // Add Fragrance Notes
            foreach (var noteId in model.SelectedFragranceNoteIds.Distinct())
            {
                product.ProductFragranceNotes.Add(new ProductFragranceNote
                {
                    Product = product,
                    FragranceNoteId = noteId,
                    AddedAt = DateTime.UtcNow
                });
            }

            // Add Product Categories
            foreach (var catId in model.SelectedCategoryIds.Distinct())
            {
                product.ProductCategories.Add(new ProductCategory
                {
                    Product = product,
                    CategoryId = catId
                });
            }

            // Add Product Sizes (Net Cost + Profit = Selling Price)
            foreach (var sizeInput in model.Sizes)
            {
                product.ProductSizes.Add(new ProductSize
                {
                    Product = product,
                    SizeLabel = sizeInput.SizeLabel,
                    CostPrice = sizeInput.CostPrice,
                    ProfitAmount = sizeInput.ProfitAmount,
                    SellingPrice = sizeInput.SellingPrice,
                    IsDefault = sizeInput.IsDefault,
                    DisplayOrder = sizeInput.DisplayOrder
                });
            }

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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error creating product {Name}", model?.Name);
            ModelState.AddModelError(string.Empty, "حدث خطأ أثناء حفظ المنتج في قاعدة البيانات: " + ex.Message);
            model.CategoriesList = await GetCategoriesSelectListAsync();
            model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
            return View(model);
        }
    }

    // GET: Products/Edit/5
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _context.Products
            .Include(p => p.TargetAudiences)
            .Include(p => p.Seasons)
            .Include(p => p.ProductFragranceNotes)
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductSizes)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        var selectedCatIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();
        if (selectedCatIds.Count == 0 && product.CategoryId > 0)
        {
            selectedCatIds.Add(product.CategoryId);
        }

        var orderedSizes = product.ProductSizes
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.DisplayOrder)
            .Select(s => new ProductSizeInputViewModel
            {
                Id = s.Id,
                SizeLabel = s.SizeLabel,
                CostPrice = s.CostPrice,
                ProfitAmount = s.ProfitAmount,
                SellingPrice = s.SellingPrice,
                IsDefault = s.IsDefault,
                DisplayOrder = s.DisplayOrder
            })
            .ToList();

        var effectiveProfit = (product.ProfitAmount > 0 || product.CostPrice > 0)
            ? product.ProfitAmount
            : product.Price;

        if (!orderedSizes.Any())
        {
            orderedSizes.Add(new ProductSizeInputViewModel
            {
                SizeLabel = "100ml",
                CostPrice = product.CostPrice,
                ProfitAmount = effectiveProfit,
                SellingPrice = product.Price,
                IsDefault = true,
                DisplayOrder = 0
            });
        }

        var model = new ProductEditViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            Description = product.Description,
            FragranceTagline = product.FragranceTagline,
            CostPrice = product.CostPrice,
            ProfitAmount = effectiveProfit,
            Price = product.Price,
            Sizes = orderedSizes,
            StockQuantity = product.StockQuantity,
            ReorderLevel = product.ReorderLevel,
            CategoryId = product.CategoryId,
            SelectedCategoryIds = selectedCatIds,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            IsBestSeller = product.IsBestSeller,
            ExistingImageUrl = product.ImageUrl,
            CategoriesList = await GetCategoriesSelectListAsync(),

            // Fragrance Discovery Profile
            Projection = product.Projection,
            InspiredBy = product.InspiredBy,
            IsFragranceProfileApproved = product.IsFragranceProfileApproved,
            FragranceProfileReviewedAt = product.FragranceProfileReviewedAt,
            SelectedAudiences = product.TargetAudiences.Select(a => a.Audience).ToList(),
            SelectedSeasons = product.Seasons.Select(s => s.Season).ToList(),
            SelectedFragranceNoteIds = product.ProductFragranceNotes.Select(n => n.FragranceNoteId).ToList(),
            AvailableNotesGrouped = await GetGroupedFragranceNotesAsync()
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

        var product = await _context.Products
            .Include(p => p.TargetAudiences)
            .Include(p => p.Seasons)
            .Include(p => p.ProductFragranceNotes)
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductSizes)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        // Null safety guards
        model.SelectedAudiences ??= new();
        model.SelectedSeasons ??= new();
        model.SelectedFragranceNoteIds ??= new();
        model.SelectedCategoryIds ??= new();

        if (model.CategoryId > 0 && !model.SelectedCategoryIds.Contains(model.CategoryId))
        {
            model.SelectedCategoryIds.Add(model.CategoryId);
        }
        else if (model.SelectedCategoryIds.Count > 0 && model.CategoryId <= 0)
        {
            model.CategoryId = model.SelectedCategoryIds.First();
        }

        if (model.SelectedCategoryIds.Count == 0 && model.CategoryId <= 0)
        {
            ModelState.AddModelError("SelectedCategoryIds", "يرجى تحديد قسم واحد على الأقل للمنتج (Please select at least one category).");
        }

        var normalizedSku = (model.Sku ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSku))
        {
            ModelState.AddModelError("Sku", "رمز SKU مطلوب.");
        }
        else if (await _context.Products.AnyAsync(p => p.Id != id && p.Sku.ToUpper() == normalizedSku))
        {
            ModelState.AddModelError("Sku", "Another product already uses this SKU code.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError("Name", "اسم المنتج مطلوب.");
        }

        if (model.ImageFile != null && !ValidateUploadedImage(model.ImageFile, out var imageError))
        {
            ModelState.AddModelError("ImageFile", imageError!);
        }

        // Detect whether fragrance profile changed
        bool fragranceProfileChanged = FragranceProfileHasChanged(product, model);

        // Validate completeness if admin checked approval
        if (model.IsFragranceProfileApproved)
        {
            ValidateFragranceProfileCompleteness(model.SelectedAudiences, model.SelectedSeasons, model.Projection, model.SelectedFragranceNoteIds);
        }

        NormalizeAndValidatePricing(model);

        if (!ModelState.IsValid)
        {
            model.ExistingImageUrl = product.ImageUrl;
            model.CategoriesList = await GetCategoriesSelectListAsync();
            model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
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
                model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
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
        product.CostPrice = model.CostPrice;
        product.ProfitAmount = model.ProfitAmount;
        product.Price = model.Price;
        product.ReorderLevel = model.ReorderLevel;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;
        product.IsFeatured = model.IsFeatured;
        product.IsBestSeller = model.IsBestSeller;

        // Fragrance Profile Update & Approval Invalidation Logic
        product.Projection = model.Projection;
        product.InspiredBy = string.IsNullOrWhiteSpace(model.InspiredBy) ? null : model.InspiredBy.Trim();

        if (model.IsFragranceProfileApproved)
        {
            // Explicit admin approval with full validation passed
            product.IsFragranceProfileApproved = true;
            if (!product.FragranceProfileReviewedAt.HasValue || fragranceProfileChanged)
            {
                product.FragranceProfileReviewedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // Reset approval if unapproved or if fragrance attributes changed without explicit approval
            product.IsFragranceProfileApproved = false;
            product.FragranceProfileReviewedAt = null;
        }

        // Synchronize Target Audiences
        var selectedAudiences = model.SelectedAudiences.Distinct().ToHashSet();
        var audiencesToRemove = product.TargetAudiences.Where(a => !selectedAudiences.Contains(a.Audience)).ToList();
        foreach (var item in audiencesToRemove) product.TargetAudiences.Remove(item);
        foreach (var aud in selectedAudiences)
        {
            if (!product.TargetAudiences.Any(a => a.Audience == aud))
            {
                product.TargetAudiences.Add(new ProductTargetAudience { ProductId = product.Id, Audience = aud });
            }
        }

        // Synchronize Seasons
        var selectedSeasons = model.SelectedSeasons.Distinct().ToHashSet();
        var seasonsToRemove = product.Seasons.Where(s => !selectedSeasons.Contains(s.Season)).ToList();
        foreach (var item in seasonsToRemove) product.Seasons.Remove(item);
        foreach (var season in selectedSeasons)
        {
            if (!product.Seasons.Any(ps => ps.Season == season))
            {
                product.Seasons.Add(new ProductSeason { ProductId = product.Id, Season = season });
            }
        }

        // Synchronize Fragrance Notes
        var selectedNotes = model.SelectedFragranceNoteIds.Distinct().ToHashSet();
        var notesToRemove = product.ProductFragranceNotes.Where(pfn => !selectedNotes.Contains(pfn.FragranceNoteId)).ToList();
        foreach (var item in notesToRemove) product.ProductFragranceNotes.Remove(item);
        foreach (var noteId in selectedNotes)
        {
            if (!product.ProductFragranceNotes.Any(pfn => pfn.FragranceNoteId == noteId))
            {
                product.ProductFragranceNotes.Add(new ProductFragranceNote
                {
                    ProductId = product.Id,
                    FragranceNoteId = noteId,
                    AddedAt = DateTime.UtcNow
                });
            }
        }

        // Synchronize Product Categories
        var distinctSelectedCatIds = model.SelectedCategoryIds.Distinct().ToHashSet();
        var catsToRemove = product.ProductCategories.Where(pc => !distinctSelectedCatIds.Contains(pc.CategoryId)).ToList();
        foreach (var item in catsToRemove) product.ProductCategories.Remove(item);
        foreach (var catId in distinctSelectedCatIds)
        {
            if (!product.ProductCategories.Any(pc => pc.CategoryId == catId))
            {
                product.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = catId });
            }
        }

        // Synchronize Product Sizes (Net Cost + Profit = Selling Price)
        var incomingIds = model.Sizes.Where(s => s.Id > 0).Select(s => s.Id).ToHashSet();
        var sizesToRemove = product.ProductSizes.Where(s => !incomingIds.Contains(s.Id)).ToList();
        foreach (var removed in sizesToRemove)
        {
            product.ProductSizes.Remove(removed);
            _context.ProductSizes.Remove(removed);
        }

        foreach (var sizeInput in model.Sizes)
        {
            var existingSize = sizeInput.Id > 0
                ? product.ProductSizes.FirstOrDefault(s => s.Id == sizeInput.Id)
                : null;

            if (existingSize != null)
            {
                existingSize.SizeLabel = sizeInput.SizeLabel;
                existingSize.CostPrice = sizeInput.CostPrice;
                existingSize.ProfitAmount = sizeInput.ProfitAmount;
                existingSize.SellingPrice = sizeInput.SellingPrice;
                existingSize.IsDefault = sizeInput.IsDefault;
                existingSize.DisplayOrder = sizeInput.DisplayOrder;
            }
            else
            {
                product.ProductSizes.Add(new ProductSize
                {
                    ProductId = product.Id,
                    SizeLabel = sizeInput.SizeLabel,
                    CostPrice = sizeInput.CostPrice,
                    ProfitAmount = sizeInput.ProfitAmount,
                    SellingPrice = sizeInput.SellingPrice,
                    IsDefault = sizeInput.IsDefault,
                    DisplayOrder = sizeInput.DisplayOrder
                });
            }
        }

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
            model.AvailableNotesGrouped = await GetGroupedFragranceNotesAsync();
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
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductSizes)
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
            TempData["ErrorMessage"] = "المنتج المطلوب حذفه غير موجود أو تم حذفه مسبقاً.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // Business Rule: If product has sales orders history, soft delete (archive) to protect order records & financial history
            var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (hasOrders)
            {
                product.IsActive = false;
                product.IsPublished = false;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                InvalidateStoreCache();

                TempData["WarningMessage"] = $"المنتج '{product.Name}' لديه سجل مبيعات وطلبات سابقة مع العملاء، تم أرشفته وتعطيله من المتجر بنجاح بدلاً من الحذف النهائي حفاظاً على سلامة الفواتير وسجلات الطلبات.";
                return RedirectToAction(nameof(Index));
            }

            // Unlink and clean up dependent references with DeleteBehavior.Restrict:
            // 1. HomeOffers (deals of the day, bundles)
            var relatedOffers = await _context.HomeOffers
                .Where(o => o.ProductId == id || o.SecondProductId == id)
                .ToListAsync();
            foreach (var offer in relatedOffers)
            {
                if (offer.ProductId == id)
                {
                    _context.HomeOffers.Remove(offer);
                }
                else if (offer.SecondProductId == id)
                {
                    offer.SecondProductId = null;
                    _context.HomeOffers.Update(offer);
                }
            }

            // 2. HomeSettings (Spotlight / Deal of the day, Best sellers / Featured lists)
            var homeSettings = await _context.HomeSettings.ToListAsync();
            foreach (var s in homeSettings)
            {
                bool changed = false;
                if (s.SpotlightProductId == id)
                {
                    s.SpotlightProductId = null;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(s.BestSellersProductIds))
                {
                    var ids = s.BestSellersProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(x => x != id.ToString())
                        .ToList();
                    var newStr = ids.Any() ? string.Join(",", ids) : null;
                    if (s.BestSellersProductIds != newStr)
                    {
                        s.BestSellersProductIds = newStr;
                        changed = true;
                    }
                }
                if (!string.IsNullOrWhiteSpace(s.FeaturedProductIds))
                {
                    var ids = s.FeaturedProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(x => x != id.ToString())
                        .ToList();
                    var newStr = ids.Any() ? string.Join(",", ids) : null;
                    if (s.FeaturedProductIds != newStr)
                    {
                        s.FeaturedProductIds = newStr;
                        changed = true;
                    }
                }
                if (changed)
                {
                    _context.HomeSettings.Update(s);
                }
            }

            // 3. InventoryTransactions (since this unpurchased product is being removed)
            var invTransactions = await _context.InventoryTransactions
                .Where(t => t.ProductId == id)
                .ToListAsync();
            if (invTransactions.Any())
            {
                _context.InventoryTransactions.RemoveRange(invTransactions);
            }

            // 4. ProductReviews
            var reviews = await _context.ProductReviews
                .Where(r => r.ProductId == id)
                .ToListAsync();
            if (reviews.Any())
            {
                _context.ProductReviews.RemoveRange(reviews);
            }

            var imageUrl = product.ImageUrl;
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            InvalidateStoreCache();

            // Safe cleanup of product image
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                if (imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
                {
                    await _mediaDeletionQueue.EnqueueAsync(imageUrl);
                }
                else
                {
                    try
                    {
                        await _imageStorageService.DeleteImageAsync(imageUrl);
                    }
                    catch (Exception imgEx)
                    {
                        _logger?.LogWarning(imgEx, "Could not delete image file {Url} for deleted product {Id}", imageUrl, id);
                    }
                }
            }

            TempData["SuccessMessage"] = $"تم حذف المنتج '{product.Name}' نهائياً بنجاح!";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete product {Id}", id);
            TempData["ErrorMessage"] = $"تعذر إتمام عملية حذف المنتج: {ex.Message}";
        }

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

    private async Task<List<FragranceNoteCategoryGroupViewModel>> GetGroupedFragranceNotesAsync()
    {
        var notes = await _context.FragranceNotes
            .Where(n => n.IsActive)
            .OrderBy(n => n.Category)
            .ThenBy(n => n.DisplayOrder)
            .ThenBy(n => n.Name)
            .Select(n => new FragranceNoteOptionViewModel
            {
                Id = n.Id,
                Name = n.Name,
                ArabicName = n.ArabicName,
                Category = n.Category
            })
            .ToListAsync();

        return notes
            .GroupBy(n => n.Category)
            .OrderBy(g => (int)g.Key)
            .Select(g => new FragranceNoteCategoryGroupViewModel
            {
                Category = g.Key,
                CategoryNameEn = GetCategoryNameEn(g.Key),
                CategoryNameAr = GetCategoryNameAr(g.Key),
                Notes = g.ToList()
            })
            .ToList();
    }

    private void ValidateFragranceProfileCompleteness(
        List<AudienceTarget>? audiences,
        List<FragranceSeason>? seasons,
        FragranceProjection? projection,
        List<int>? noteIds)
    {
        if (audiences == null || !audiences.Any())
        {
            ModelState.AddModelError("SelectedAudiences", "To approve this product for fragrance recommendations, at least one Target Audience must be selected.");
        }
        if (seasons == null || !seasons.Any())
        {
            ModelState.AddModelError("SelectedSeasons", "To approve this product for fragrance recommendations, at least one Season must be selected.");
        }
        if (!projection.HasValue)
        {
            ModelState.AddModelError("Projection", "To approve this product for fragrance recommendations, a Projection intensity must be selected.");
        }
        if (noteIds == null || !noteIds.Any())
        {
            ModelState.AddModelError("SelectedFragranceNoteIds", "To approve this product for fragrance recommendations, at least one Fragrance Note must be selected.");
        }
    }

    private static bool FragranceProfileHasChanged(Product product, ProductEditViewModel model)
    {
        var currentAudiences = product.TargetAudiences.Select(a => a.Audience).ToHashSet();
        var newAudiences = model.SelectedAudiences.ToHashSet();
        if (!currentAudiences.SetEquals(newAudiences)) return true;

        var currentSeasons = product.Seasons.Select(s => s.Season).ToHashSet();
        var newSeasons = model.SelectedSeasons.ToHashSet();
        if (!currentSeasons.SetEquals(newSeasons)) return true;

        var currentNotes = product.ProductFragranceNotes.Select(n => n.FragranceNoteId).ToHashSet();
        var newNotes = model.SelectedFragranceNoteIds.ToHashSet();
        if (!currentNotes.SetEquals(newNotes)) return true;

        if (product.Projection != model.Projection) return true;

        var currentInspired = (product.InspiredBy ?? string.Empty).Trim();
        var newInspired = (model.InspiredBy ?? string.Empty).Trim();
        if (!string.Equals(currentInspired, newInspired, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
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

    #endregion
}
