using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stockify.Data;
using Stockify.DTOs.FragranceDiscovery;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;

namespace Stockify.Controllers;

public class FragranceDiscoveryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly IFragranceRecommendationService _recommendationService;
    private readonly ILogger<FragranceDiscoveryController>? _logger;

    private const string QuizDataCacheKey = "roven_fragrance_discovery_quiz_data";

    public FragranceDiscoveryController(
        ApplicationDbContext context,
        IMemoryCache memoryCache,
        IFragranceRecommendationService recommendationService,
        ILogger<FragranceDiscoveryController>? logger = null)
    {
        _context = context;
        _memoryCache = memoryCache;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// Computes personalized perfume recommendations based on customer quiz responses.
    /// Evaluates all eligible database products using weighted normalized matching.
    /// </summary>
    [HttpPost("/api/fragrance-discovery/recommendations")]
    public async Task<IActionResult> GetRecommendations([FromBody] FragranceRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new FragranceRecommendationResponse
            {
                Success = false,
                Message = "Invalid recommendation request payload."
            });
        }

        try
        {
            var response = await _recommendationService.GetRecommendationsAsync(request, cancellationToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calculating fragrance recommendations");
            return StatusCode(500, new FragranceRecommendationResponse
            {
                Success = false,
                Message = "An error occurred while evaluating fragrance recommendations. Please try again."
            });
        }
    }

    /// <summary>
    /// Returns active fragrance notes, categories, and quick picks for the customer discovery quiz.
    /// Cached in memory for high-performance storefront requests.
    /// </summary>
    [HttpGet("/api/fragrance-discovery/quiz-data")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetQuizData()
    {
        var quizData = await _memoryCache.GetOrCreateAsync(QuizDataCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            // 1. Fetch active notes
            var notes = await _context.FragranceNotes
                .Where(n => n.IsActive)
                .OrderBy(n => n.Category)
                .ThenBy(n => n.DisplayOrder)
                .ThenBy(n => n.Name)
                .Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.ArabicName,
                    Category = (int)n.Category,
                    CategoryNameEn = GetCategoryNameEn(n.Category),
                    CategoryNameAr = GetCategoryNameAr(n.Category),
                    n.DisplayOrder
                })
                .ToListAsync();

            // 2. Fetch Loved Notes Quick Picks (Question 2)
            var lovedQuickPicks = await _context.FragranceNoteQuickPicks
                .Include(qp => qp.FragranceNote)
                .Where(qp => qp.Context == QuickPickContext.Love && qp.FragranceNote.IsActive)
                .OrderBy(qp => qp.DisplayOrder)
                .ThenBy(qp => qp.FragranceNote.Name)
                .Select(qp => new
                {
                    qp.FragranceNoteId,
                    qp.FragranceNote.Name,
                    qp.FragranceNote.ArabicName,
                    Category = (int)qp.FragranceNote.Category,
                    qp.DisplayOrder
                })
                .ToListAsync();

            // 3. Fetch Avoided Notes Quick Picks (Question 3)
            var avoidedQuickPicks = await _context.FragranceNoteQuickPicks
                .Include(qp => qp.FragranceNote)
                .Where(qp => qp.Context == QuickPickContext.Avoid && qp.FragranceNote.IsActive)
                .OrderBy(qp => qp.DisplayOrder)
                .ThenBy(qp => qp.FragranceNote.Name)
                .Select(qp => new
                {
                    qp.FragranceNoteId,
                    qp.FragranceNote.Name,
                    qp.FragranceNote.ArabicName,
                    Category = (int)qp.FragranceNote.Category,
                    qp.DisplayOrder
                })
                .ToListAsync();

            // 4. Categories list
            var categories = new[]
            {
                new { Id = (int)FragranceNoteCategory.Citrus, NameEn = "Citrus", NameAr = "حمضيات", Icon = "fa-lemon", Color = "#E6A23C" },
                new { Id = (int)FragranceNoteCategory.Floral, NameEn = "Floral", NameAr = "زهور", Icon = "fa-spa", Color = "#E86F88" },
                new { Id = (int)FragranceNoteCategory.Woody, NameEn = "Woody", NameAr = "أخشاب", Icon = "fa-tree", Color = "#8D6E63" },
                new { Id = (int)FragranceNoteCategory.OrientalAmber, NameEn = "Oriental / Amber", NameAr = "شرقي وعنبر", Icon = "fa-gem", Color = "#D4AF37" },
                new { Id = (int)FragranceNoteCategory.FreshAromatic, NameEn = "Fresh / Aromatic", NameAr = "منعش وأروماتيك", Icon = "fa-wind", Color = "#4DB6AC" },
                new { Id = (int)FragranceNoteCategory.GourmandSweet, NameEn = "Gourmand / Sweet", NameAr = "غورماند وحلويات", Icon = "fa-candy-cane", Color = "#BA68C8" }
            };

            return new
            {
                success = true,
                totalNotes = notes.Count,
                categories,
                lovedQuickPicks,
                avoidedQuickPicks,
                notes
            };
        });

        return Json(quizData);
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
