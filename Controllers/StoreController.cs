using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.ViewModels.Store;

namespace Stockify.Controllers;

public class StoreController : Controller
{
    private readonly ApplicationDbContext _context;

    // Brand aliases and common phonetic misspellings for search expansion
    private static readonly HashSet<string> BrandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "roven", "rovn", "rovin", "rowen", "rovan", "rouven", "rofen", "roveen", "rovyn", "rovenn", "rov",
        "روڤين", "روفين", "روفيين", "روفن", "روڤن", "روبن", "روفيان",
        "عطور", "براند", "عطورات", "عطر", "parfumerie", "maison", "perfumes", "perfume",
        "براند روفين", "براند روڤين", "عطور روفين", "عطور روڤين", "ميزون روفين", "ميزون روڤين",
        "roven perfumes", "roven perfume", "maison roven", "roven brand"
    };

    private static string NormalizeArabic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.ToLowerInvariant()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا')
            .Replace('ة', 'ه')
            .Replace('ى', 'ي')
            .Replace('ڤ', 'ف')
            .Trim();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    public StoreController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Store or /Store?categoryId=2&search=Dell&sort=price_asc&inStockOnly=true
    public async Task<IActionResult> Index(int? categoryId, string? search, string? sort = "newest", bool inStockOnly = false)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsPublished)
            .AsQueryable();

        // Filter by Category
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        string? searchCorrectionNotice = null;

        // Search Term (Name, SKU, Description, Category) with Typo & Brand Alias Tolerance
        if (!string.IsNullOrWhiteSpace(search))
        {
            var rawTerm = search.Trim();
            var normalizedTerm = NormalizeArabic(rawTerm);

            // 1. Is this a search for the ROVEN brand name or its misspellings/aliases?
            if (BrandAliases.Contains(rawTerm) || BrandAliases.Contains(normalizedTerm))
            {
                // The customer searched for the brand itself (e.g. "roven", "rovn", "روفين", "روبن")
                // In the official boutique, all products belong to the brand!
                searchCorrectionNotice = $"عرض كافة عطور دار Maison ROVEN الفاخرة (مطابقة لبحث: \"{rawTerm}\")";
            }
            else
            {
                // 2. Check if the query contains a brand keyword plus specific notes/names (e.g. "roven noir", "عطر روفين عود")
                var words = rawTerm.Split(new[] { ' ', '+', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                var specificWords = words.Where(w => !BrandAliases.Contains(w) && !BrandAliases.Contains(NormalizeArabic(w))).ToList();

                var effectiveTerms = specificWords.Count > 0 ? specificWords : words.ToList();
                if (specificWords.Count > 0 && specificWords.Count < words.Length)
                {
                    searchCorrectionNotice = $"عرض عطور ROVEN المطابقة لـ: \"{string.Join(" ", specificWords)}\"";
                }

                foreach (var term in effectiveTerms)
                {
                    var tLower = term.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(tLower) ||
                        (p.Sku != null && p.Sku.ToLower().Contains(tLower)) ||
                        (p.Description != null && p.Description.ToLower().Contains(tLower)) ||
                        (p.Category != null && p.Category.Name.ToLower().Contains(tLower)));
                }
            }
        }

        // In-Stock Only filter
        if (inStockOnly)
        {
            query = query.Where(p => p.StockQuantity > 0);
        }

        // Sorting
        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var products = await query
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .ToListAsync();

        // 3. Typo Fallback: If 0 results found for a fragrance search, run in-memory fuzzy matching
        if (products.Count == 0 && !string.IsNullOrWhiteSpace(search))
        {
            var rawTerm = search.Trim();
            var allCandidates = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.IsPublished)
                .Where(p => !categoryId.HasValue || p.CategoryId == categoryId.Value)
                .Where(p => !inStockOnly || p.StockQuantity > 0)
                .ToListAsync();

            var searchTokens = rawTerm.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .ToList();

            var fuzzyMatches = new List<Product>();

            foreach (var candidate in allCandidates)
            {
                var targetText = $"{candidate.Name} {candidate.Description} {candidate.Category?.Name}".ToLowerInvariant();
                var targetWords = targetText.Split(new[] { ' ', ',', '.', '(', ')', '-', '—', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                bool isMatch = false;
                foreach (var sToken in searchTokens)
                {
                    foreach (var tWord in targetWords)
                    {
                        if (Math.Abs(sToken.Length - tWord.Length) > 2) continue;

                        int maxDist = sToken.Length <= 4 ? 1 : 2;
                        if (LevenshteinDistance(sToken, tWord) <= maxDist)
                        {
                            isMatch = true;
                            break;
                        }
                    }
                    if (isMatch) break;
                }

                if (isMatch)
                {
                    fuzzyMatches.Add(candidate);
                }
            }

            if (fuzzyMatches.Count > 0)
            {
                products = fuzzyMatches.Select(p => new StoreProductCardViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku ?? "",
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : "General",
                    IsFeatured = p.IsFeatured
                }).ToList();

                searchCorrectionNotice = $"عرض النتائج المقترحة الأقرب لبحثك: \"{rawTerm}\"";
            }
        }

        var totalCount = products.Count;

        var categories = await _context.Categories
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductsCount = c.Products.Count(p => p.IsActive && p.IsPublished)
            })
            .OrderBy(c => c.Name)
            .ToListAsync();

        string? selectedCategoryName = null;
        if (categoryId.HasValue)
        {
            selectedCategoryName = categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name;
        }

        var model = new StoreCatalogViewModel
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            SelectedCategoryName = selectedCategoryName,
            SearchTerm = search,
            SearchCorrectionNotice = searchCorrectionNotice,
            CurrentSort = sort ?? "newest",
            InStockOnly = inStockOnly,
            TotalCount = totalCount
        };

        return View(model);
    }

    // GET: /Store/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Id == id && p.IsActive && p.IsPublished)
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound();
        }

        // Related Products from the same category
        var related = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsActive && p.IsPublished)
            .OrderByDescending(p => p.CreatedAt)
            .Take(4)
            .Select(p => new StoreProductCardViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku ?? "",
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                IsFeatured = p.IsFeatured
            })
            .ToListAsync();

        // Supplement up to 4 recommendations if category has fewer products
        if (related.Count < 4)
        {
            var existingIds = related.Select(r => r.Id).Append(product.Id).ToList();
            var needed = 4 - related.Count;
            var additional = await _context.Products
                .Include(p => p.Category)
                .Where(p => !existingIds.Contains(p.Id) && p.IsActive && p.IsPublished)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt)
                .Take(needed)
                .Select(p => new StoreProductCardViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku ?? "",
                    Description = p.Description,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    ImageUrl = p.ImageUrl,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : "General",
                    IsFeatured = p.IsFeatured
                })
                .ToListAsync();

            related.AddRange(additional);
        }

        // Approved reviews for this product
        var reviews = await _context.ProductReviews
            .Where(r => r.ProductId == id && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ProductReviewViewModel
            {
                Id = r.Id,
                CustomerName = r.CustomerName,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var model = new StoreProductDetailsViewModel
        {
            Product = product,
            RelatedProducts = related,
            Reviews = reviews
        };

        return View(model);
    }

    // POST: /Store/SubmitReview
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(int productId, string customerName, int rating, string comment, string? customerEmail, string? customerPhone)
    {
        if (productId <= 0)
        {
            return Json(new { success = false, message = "معرّف المنتج غير صالح." });
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return Json(new { success = false, message = "يرجى كتابة اسمك الكريم لإتمام التقييم." });
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return Json(new { success = false, message = "يرجى كتابة رأيك أو تعليقك على هذا العطر." });
        }

        if (rating < 1 || rating > 5)
        {
            rating = 5;
        }

        var productExists = await _context.Products.AnyAsync(p => p.Id == productId && p.IsActive);
        if (!productExists)
        {
            return Json(new { success = false, message = "المنتج المطلوب تقييمه غير متاح حالياً." });
        }

        var review = new ProductReview
        {
            ProductId = productId,
            CustomerName = customerName.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim(),
            Rating = rating,
            Comment = comment.Trim(),
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductReviews.Add(review);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "شكراً لتقييمك! تم استلام تقييمك بنجاح وسيظهر في صفحة العطر فور اعتماده من إدارة روڤين."
        });
    }
}

