namespace Stockify.DTOs.FragranceDiscovery;

/// <summary>
/// Root recommendation response containing ranked matches and metadata.
/// </summary>
public class FragranceRecommendationResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public int TotalEligibleProducts { get; set; }
    public int TotalMatchesFound { get; set; }
    public List<FragranceRecommendationItemDto> Recommendations { get; set; } = new();
    public RecommendationCustomerSummaryDto CustomerPreferences { get; set; } = new();
}

/// <summary>
/// Individual recommended perfume product with calculated scoring and match explanations.
/// </summary>
public class FragranceRecommendationItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public string? FragranceTagline { get; set; }
    public string? InspiredBy { get; set; }
    
    /// <summary>
    /// ROVEN perfumes have ONE fixed bottle size (50ml).
    /// </summary>
    public string BottleSize { get; set; } = "50ml";

    // Scoring & Ranking
    public int Rank { get; set; }
    public int MatchPercentage { get; set; }
    public double RawCompatibilityScore { get; set; }
    public double PositiveScorePercentage { get; set; }
    public double AvoidPenaltyPoints { get; set; }

    // Detailed points breakdown
    public ScoreBreakdownDto ScoreBreakdown { get; set; } = new();

    // Notes Breakdown
    public List<string> MatchedLovedNotes { get; set; } = new();
    public List<string> MatchedAvoidedNotes { get; set; } = new();
    public List<string> AllProductNotes { get; set; } = new();

    // Verifiable explanations derived from real product metadata
    public List<string> MatchExplanations { get; set; } = new();
}

/// <summary>
/// Mathematical scoring breakdown across all dimensions.
/// </summary>
public class ScoreBreakdownDto
{
    public double AudienceEarned { get; set; }
    public double AudienceMax { get; set; } = 25.0;

    public double LovedNotesEarned { get; set; }
    public double LovedNotesMax { get; set; } = 45.0; // 0 if skipped

    public double SeasonEarned { get; set; }
    public double SeasonMax { get; set; } = 15.0;

    public double ProjectionEarned { get; set; }
    public double ProjectionMax { get; set; } = 15.0;

    public double ApplicableMaxPoints { get; set; }
    public double EarnedPositivePoints { get; set; }
    public double AvoidPenaltyPoints { get; set; }
}

/// <summary>
/// Verified customer quiz inputs echoed back for frontend context.
/// </summary>
public class RecommendationCustomerSummaryDto
{
    public string Direction { get; set; } = string.Empty;
    public int LovedNotesCount { get; set; }
    public int AvoidedNotesCount { get; set; }
    public List<string> SelectedSeasons { get; set; } = new();
    public string Projection { get; set; } = string.Empty;
}
