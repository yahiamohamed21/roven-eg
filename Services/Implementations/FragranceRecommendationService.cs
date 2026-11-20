using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.DTOs.FragranceDiscovery;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

/// <summary>
/// Production recommendation intelligence engine for ROVEN Find Your Scent.
/// Evaluates customer fragrance preferences against all eligible database products using
/// normalized multi-dimensional scoring and verified product metadata.
/// </summary>
public class FragranceRecommendationService : IFragranceRecommendationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FragranceRecommendationService>? _logger;

    public FragranceRecommendationService(
        ApplicationDbContext context,
        ILogger<FragranceRecommendationService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FragranceRecommendationResponse> GetRecommendationsAsync(
        FragranceRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Strict Server-Side Validation
        var validation = await ValidateRequestAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new FragranceRecommendationResponse
            {
                Success = false,
                Message = validation.ErrorMessage,
                TotalEligibleProducts = 0,
                TotalMatchesFound = 0,
                Recommendations = new List<FragranceRecommendationItemDto>()
            };
        }

        var (userAudience, userSeasons, userProjection) = validation.ParsedPreferences;

        // 2. Query Eligible Database Products
        // A product is eligible if:
        // - Active in catalog (IsActive)
        // - Published on storefront (IsPublished)
        // - Available in inventory (StockQuantity > 0)
        // - Fragrance discovery profile explicitly approved (IsFragranceProfileApproved)
        // - Has defined projection level (Projection.HasValue)
        // - Has at least one target audience
        // - Has at least one valid season
        // - Has at least one active fragrance note
        var eligibleProducts = await _context.Products
            .AsNoTracking()
            .Include(p => p.TargetAudiences)
            .Include(p => p.Seasons)
            .Include(p => p.ProductFragranceNotes)
                .ThenInclude(pfn => pfn.FragranceNote)
            .Where(p => p.IsActive
                     && p.IsPublished
                     && p.StockQuantity > 0
                     && p.IsFragranceProfileApproved
                     && p.Projection.HasValue
                     && p.TargetAudiences.Any()
                     && p.Seasons.Any()
                     && p.ProductFragranceNotes.Any(pfn => pfn.FragranceNote.IsActive))
            .ToListAsync(cancellationToken);

        int totalEligible = eligibleProducts.Count;
        var scoredItems = new List<FragranceRecommendationItemDto>();

        // 3. Evaluate Each Eligible Product
        foreach (var product in eligibleProducts)
        {
            var item = EvaluateProduct(product, request, userAudience, userSeasons, userProjection);

            // Filter: Must achieve at least 50% final compatibility
            if (item.MatchPercentage >= 50)
            {
                scoredItems.Add(item);
            }
        }

        // 4. Deterministic Ranking
        // Primary: Match percentage descending
        // Secondary: Positive score percentage descending (prioritizes higher raw match before penalties)
        // Tertiary: Number of matched loved notes descending
        // Quaternary: Product ID ascending (stable deterministic tie-breaker)
        var rankedRecommendations = scoredItems
            .OrderByDescending(r => r.MatchPercentage)
            .ThenByDescending(r => r.PositiveScorePercentage)
            .ThenByDescending(r => r.MatchedLovedNotes.Count)
            .ThenBy(r => r.ProductId)
            .ToList();

        // Assign 1-based ranks
        for (int i = 0; i < rankedRecommendations.Count; i++)
        {
            rankedRecommendations[i].Rank = i + 1;
        }

        string responseMessage = rankedRecommendations.Count > 0
            ? $"Found {rankedRecommendations.Count} matching fragrances tailored to your preferences."
            : "No fragrances met the 50% compatibility threshold for your selected preferences. Try exploring broader notes or seasons.";

        return new FragranceRecommendationResponse
        {
            Success = true,
            Message = responseMessage,
            TotalEligibleProducts = totalEligible,
            TotalMatchesFound = rankedRecommendations.Count,
            Recommendations = rankedRecommendations,
            CustomerPreferences = new RecommendationCustomerSummaryDto
            {
                Direction = userAudience.ToString(),
                LovedNotesCount = request.LovedNoteIds.Count,
                AvoidedNotesCount = request.AvoidedNoteIds.Count,
                SelectedSeasons = userSeasons.Select(s => s.ToString()).ToList(),
                Projection = userProjection.ToString()
            }
        };
    }

    /// <summary>
    /// Evaluates a single eligible product against the customer preferences.
    /// </summary>
    public static FragranceRecommendationItemDto EvaluateProduct(
        Product product,
        FragranceRecommendationRequest request,
        AudienceTarget userAudience,
        HashSet<FragranceSeason> userSeasons,
        FragranceProjection userProjection)
    {
        var prodAudiences = product.TargetAudiences.Select(a => a.Audience).ToHashSet();
        var prodSeasons = product.Seasons.Select(s => s.Season).ToHashSet();
        var prodActiveNotes = product.ProductFragranceNotes
            .Where(pfn => pfn.FragranceNote.IsActive)
            .Select(pfn => pfn.FragranceNote)
            .ToList();
        var prodNoteIds = prodActiveNotes.Select(n => n.Id).ToHashSet();

        // -------------------------------------------------------------
        // A. Target Audience (25 Points)
        // -------------------------------------------------------------
        double audienceScore = 0.0;
        const double audienceMax = 25.0;
        bool isAudienceMatch = false;

        if (userAudience == AudienceTarget.ForHim)
        {
            // For Him matches product with ForHim or Unisex
            isAudienceMatch = prodAudiences.Contains(AudienceTarget.ForHim) || prodAudiences.Contains(AudienceTarget.Unisex);
        }
        else if (userAudience == AudienceTarget.ForHer)
        {
            // For Her matches product with ForHer or Unisex
            isAudienceMatch = prodAudiences.Contains(AudienceTarget.ForHer) || prodAudiences.Contains(AudienceTarget.Unisex);
        }
        else if (userAudience == AudienceTarget.Unisex)
        {
            // Unisex matches product with Unisex, or products covering both ForHim and ForHer
            isAudienceMatch = prodAudiences.Contains(AudienceTarget.Unisex)
                           || (prodAudiences.Contains(AudienceTarget.ForHim) && prodAudiences.Contains(AudienceTarget.ForHer));
        }

        if (isAudienceMatch)
        {
            audienceScore = audienceMax;
        }

        // -------------------------------------------------------------
        // B. Notes You Love (45 Points)
        // -------------------------------------------------------------
        double lovedNotesScore = 0.0;
        double lovedNotesMax = 0.0;
        int lovedCount = request.LovedNoteIds.Count;
        var matchedLovedNotesList = new List<string>();

        if (lovedCount > 0)
        {
            lovedNotesMax = 45.0;
            var matchedNotes = prodActiveNotes.Where(n => request.LovedNoteIds.Contains(n.Id)).ToList();
            matchedLovedNotesList = matchedNotes.Select(n => n.Name).OrderBy(n => n).ToList();
            int matchedLovedCount = matchedNotes.Count;

            lovedNotesScore = ((double)matchedLovedCount / lovedCount) * 45.0;
        }
        // If lovedCount == 0 (skipped step), lovedNotesMax = 0 (excluded from denominator)

        // -------------------------------------------------------------
        // C. Season Compatibility (15 Points)
        // -------------------------------------------------------------
        double seasonScore = 0.0;
        const double seasonMax = 15.0;
        bool prodHasAllYear = prodSeasons.Contains(FragranceSeason.AllYear);
        bool userHasAllYear = userSeasons.Contains(FragranceSeason.AllYear);

        if (prodHasAllYear && userHasAllYear)
        {
            // Exact AllYear match
            seasonScore = 15.0;
        }
        else if (prodHasAllYear && !userHasAllYear)
        {
            // Product classified as All Year: 15 points for an individual season preference
            seasonScore = 15.0;
        }
        else if (!prodHasAllYear && userHasAllYear)
        {
            // Customer selects All Year and product covers at least two individual seasons: 10 points
            int individualSeasonCount = prodSeasons.Count(s => s != FragranceSeason.AllYear);
            if (individualSeasonCount >= 2)
            {
                seasonScore = 10.0;
            }
            else
            {
                seasonScore = 0.0;
            }
        }
        else
        {
            // Both are individual seasons: check for common seasons
            bool hasSharedSeason = userSeasons.Any(s => prodSeasons.Contains(s));
            if (hasSharedSeason)
            {
                seasonScore = 15.0;
            }
            else
            {
                seasonScore = 0.0;
            }
        }

        // -------------------------------------------------------------
        // D. Projection (15 Points)
        // -------------------------------------------------------------
        double projectionScore = 0.0;
        const double projectionMax = 15.0;
        int userProjLevel = (int)userProjection;
        int prodProjLevel = (int)product.Projection!.Value;
        int projDiff = Math.Abs(userProjLevel - prodProjLevel);

        if (projDiff == 0)
        {
            projectionScore = 15.0; // Exact match
        }
        else if (projDiff == 1)
        {
            projectionScore = 9.0;  // One level difference
        }
        else
        {
            projectionScore = 0.0;  // Two levels difference
        }

        // -------------------------------------------------------------
        // E. Avoided Notes Penalty (25 Points per matched avoided note)
        // -------------------------------------------------------------
        var matchedAvoidedNotesList = new List<string>();
        double avoidPenalty = 0.0;

        if (request.AvoidedNoteIds.Count > 0)
        {
            var matchedAvoided = prodActiveNotes.Where(n => request.AvoidedNoteIds.Contains(n.Id)).ToList();
            matchedAvoidedNotesList = matchedAvoided.Select(n => n.Name).OrderBy(n => n).ToList();
            avoidPenalty = matchedAvoided.Count * 25.0;
        }

        // -------------------------------------------------------------
        // F. Score Normalization
        // -------------------------------------------------------------
        double earnedPositive = audienceScore + lovedNotesScore + seasonScore + projectionScore;
        double applicableMax = audienceMax + lovedNotesMax + seasonMax + projectionMax;

        double positivePercentage = applicableMax > 0
            ? (earnedPositive / applicableMax) * 100.0
            : 0.0;

        double rawFinalScore = Math.Clamp(positivePercentage - avoidPenalty, 0.0, 100.0);
        int finalPercentage = (int)Math.Round(rawFinalScore, MidpointRounding.AwayFromZero);

        // -------------------------------------------------------------
        // G. Verified Explanations Generation
        // -------------------------------------------------------------
        var explanations = GenerateExplanations(
            product,
            isAudienceMatch,
            userAudience,
            matchedLovedNotesList,
            lovedCount,
            matchedAvoidedNotesList,
            request.AvoidedNoteIds.Count,
            seasonScore,
            prodHasAllYear,
            userHasAllYear,
            userSeasons,
            prodSeasons,
            projDiff,
            userProjection);

        return new FragranceRecommendationItemDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            ImageUrl = product.ImageUrl,
            Price = product.Price,
            FragranceTagline = product.FragranceTagline ?? "Extrait de Parfum • Maison ROVEN",
            InspiredBy = product.InspiredBy,
            BottleSize = "50ml", // ROVEN perfumes have ONE fixed bottle size
            MatchPercentage = finalPercentage,
            RawCompatibilityScore = Math.Round(rawFinalScore, 1),
            PositiveScorePercentage = Math.Round(positivePercentage, 1),
            AvoidPenaltyPoints = avoidPenalty,
            ScoreBreakdown = new ScoreBreakdownDto
            {
                AudienceEarned = audienceScore,
                AudienceMax = audienceMax,
                LovedNotesEarned = Math.Round(lovedNotesScore, 1),
                LovedNotesMax = lovedNotesMax,
                SeasonEarned = seasonScore,
                SeasonMax = seasonMax,
                ProjectionEarned = projectionScore,
                ProjectionMax = projectionMax,
                ApplicableMaxPoints = applicableMax,
                EarnedPositivePoints = Math.Round(earnedPositive, 1),
                AvoidPenaltyPoints = avoidPenalty
            },
            MatchedLovedNotes = matchedLovedNotesList,
            MatchedAvoidedNotes = matchedAvoidedNotesList,
            AllProductNotes = prodActiveNotes.Select(n => n.Name).OrderBy(n => n).ToList(),
            MatchExplanations = explanations
        };
    }

    /// <summary>
    /// Generates clear, truthful explanations based strictly on actual product attributes.
    /// </summary>
    private static List<string> GenerateExplanations(
        Product product,
        bool isAudienceMatch,
        AudienceTarget userAudience,
        List<string> matchedLovedNotes,
        int totalLovedRequested,
        List<string> matchedAvoidedNotes,
        int totalAvoidedRequested,
        double seasonScore,
        bool prodHasAllYear,
        bool userHasAllYear,
        HashSet<FragranceSeason> userSeasons,
        HashSet<FragranceSeason> prodSeasons,
        int projDiff,
        FragranceProjection userProjection)
    {
        var explanations = new List<string>();

        // 1. Loved Notes Explanation
        if (matchedLovedNotes.Count > 0)
        {
            string noteList = string.Join(" & ", matchedLovedNotes.Take(3));
            if (matchedLovedNotes.Count > 3)
            {
                noteList += $" and {matchedLovedNotes.Count - 3} more";
            }
            explanations.Add($"Matches your love for {noteList}.");
        }
        else if (totalLovedRequested > 0)
        {
            explanations.Add("Features a distinct olfactive profile outside your selected loved notes.");
        }

        // 2. Avoided Notes Explanation
        if (matchedAvoidedNotes.Count > 0)
        {
            explanations.Add($"Contains avoided note: {string.Join(", ", matchedAvoidedNotes)} (-{matchedAvoidedNotes.Count * 25}% penalty).");
        }
        else if (totalAvoidedRequested > 0)
        {
            explanations.Add("Completely free of notes you prefer to avoid.");
        }

        // 3. Audience Explanation
        if (isAudienceMatch)
        {
            string audDesc = string.Join(" / ", product.TargetAudiences.Select(a => a.Audience switch
            {
                AudienceTarget.ForHim => "For Him",
                AudienceTarget.ForHer => "For Her",
                AudienceTarget.Unisex => "Unisex",
                _ => a.Audience.ToString()
            }));
            explanations.Add($"Tailored for your selected orientation ({audDesc}).");
        }

        // 4. Season Explanation
        if (prodHasAllYear)
        {
            explanations.Add("Versatile All-Year signature profile suitable in any weather.");
        }
        else if (seasonScore == 15.0)
        {
            var matchedSeasons = userSeasons.Intersect(prodSeasons).Select(s => s.ToString()).ToList();
            string seasonsStr = matchedSeasons.Count > 0 ? string.Join(" & ", matchedSeasons) : "preferred";
            explanations.Add($"Suitable for your preferred {seasonsStr} season.");
        }
        else if (seasonScore == 10.0)
        {
            int indCount = prodSeasons.Count(s => s != FragranceSeason.AllYear);
            explanations.Add($"Spans {indCount} distinct seasons for versatile year-round wearing.");
        }

        // 5. Projection Explanation
        if (projDiff == 0)
        {
            explanations.Add($"Matches your preferred {userProjection} projection.");
        }
        else if (projDiff == 1)
        {
            explanations.Add($"Offers {product.Projection} projection (adjacent to your {userProjection} preference).");
        }

        return explanations;
    }

    /// <summary>
    /// Independent server-side request validator.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage, (AudienceTarget, HashSet<FragranceSeason>, FragranceProjection) ParsedPreferences)>
        ValidateRequestAsync(FragranceRecommendationRequest request, CancellationToken cancellationToken)
    {
        // 1. Target Audience validation
        if (string.IsNullOrWhiteSpace(request.Direction) ||
            !Enum.TryParse<AudienceTarget>(request.Direction.Trim(), true, out var userAudience))
        {
            return (false, "Invalid or missing target audience direction. Must be ForHim, ForHer, or Unisex.", default);
        }

        // 2. Projection validation
        if (string.IsNullOrWhiteSpace(request.Projection) ||
            !Enum.TryParse<FragranceProjection>(request.Projection.Trim(), true, out var userProjection))
        {
            return (false, "Invalid or missing projection intensity. Must be Moderate, Strong, or Enormous.", default);
        }

        // 3. Seasons validation
        if (request.Seasons == null || request.Seasons.Count == 0)
        {
            return (false, "At least one season or All Year must be selected.", default);
        }

        var parsedSeasons = new HashSet<FragranceSeason>();
        foreach (var sStr in request.Seasons)
        {
            if (string.IsNullOrWhiteSpace(sStr) ||
                !Enum.TryParse<FragranceSeason>(sStr.Trim(), true, out var season))
            {
                return (false, $"Invalid season specified: '{sStr}'.", default);
            }

            if (!parsedSeasons.Add(season))
            {
                return (false, $"Duplicate season specified: '{sStr}'.", default);
            }
        }

        // Enforce AllYear exclusivity
        if (parsedSeasons.Contains(FragranceSeason.AllYear) && parsedSeasons.Count > 1)
        {
            return (false, "All Year is exclusive and cannot be combined with individual seasons.", default);
        }

        // 4. Duplicate checks within Loved and Avoided notes
        if (request.LovedNoteIds != null && request.LovedNoteIds.Count != request.LovedNoteIds.Distinct().Count())
        {
            return (false, "Duplicate fragrance note IDs found in loved notes selection.", default);
        }

        if (request.AvoidedNoteIds != null && request.AvoidedNoteIds.Count != request.AvoidedNoteIds.Distinct().Count())
        {
            return (false, "Duplicate fragrance note IDs found in avoided notes selection.", default);
        }

        // 5. Cross-conflict check: Loved vs Avoided
        if (request.LovedNoteIds != null && request.AvoidedNoteIds != null)
        {
            var conflicts = request.LovedNoteIds.Intersect(request.AvoidedNoteIds).ToList();
            if (conflicts.Count > 0)
            {
                return (false, $"Fragrance note ID(s) [{string.Join(", ", conflicts)}] cannot appear in both Loved and Avoided selections.", default);
            }
        }

        // 6. Database Verification of Fragrance Note IDs
        var allRequestedNoteIds = (request.LovedNoteIds ?? Enumerable.Empty<int>())
            .Concat(request.AvoidedNoteIds ?? Enumerable.Empty<int>())
            .Distinct()
            .ToList();

        if (allRequestedNoteIds.Count > 0)
        {
            var validActiveNoteIds = await _context.FragranceNotes
                .AsNoTracking()
                .Where(n => n.IsActive && allRequestedNoteIds.Contains(n.Id))
                .Select(n => n.Id)
                .ToListAsync(cancellationToken);

            var invalidIds = allRequestedNoteIds.Except(validActiveNoteIds).ToList();
            if (invalidIds.Count > 0)
            {
                return (false, $"Invalid or inactive fragrance note ID(s): [{string.Join(", ", invalidIds)}].", default);
            }
        }

        return (true, null, (userAudience, parsedSeasons, userProjection));
    }
}
