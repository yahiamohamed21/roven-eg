using System.ComponentModel.DataAnnotations;

namespace Stockify.DTOs.FragranceDiscovery;

/// <summary>
/// Customer quiz answers payload for the fragrance recommendation engine.
/// </summary>
public class FragranceRecommendationRequest
{
    /// <summary>
    /// Step 1: Target olfactive direction ("ForHim", "ForHer", "Unisex").
    /// </summary>
    [Required(ErrorMessage = "Target audience direction is required.")]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Step 2: Fragrance note IDs that the customer loves (optional).
    /// </summary>
    public List<int> LovedNoteIds { get; set; } = new();

    /// <summary>
    /// Step 3: Fragrance note IDs that the customer wants to avoid (optional).
    /// </summary>
    public List<int> AvoidedNoteIds { get; set; } = new();

    /// <summary>
    /// Step 4: Preferred seasons ("Spring", "Summer", "Fall", "Winter", "AllYear").
    /// "AllYear" is exclusive.
    /// </summary>
    [Required(ErrorMessage = "At least one season or All Year must be selected.")]
    public List<string> Seasons { get; set; } = new();

    /// <summary>
    /// Step 5: Desired sillage & projection intensity ("Moderate", "Strong", "Enormous").
    /// </summary>
    [Required(ErrorMessage = "Preferred projection intensity is required.")]
    public string Projection { get; set; } = string.Empty;
}
