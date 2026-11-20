using Stockify.DTOs.FragranceDiscovery;

namespace Stockify.Services.Interfaces;

/// <summary>
/// Service responsible for evaluating customer fragrance quiz answers,
/// calculating weighted compatibility scores against the dynamic product catalog,
/// and returning ranked recommendations with verifiable match explanations.
/// </summary>
public interface IFragranceRecommendationService
{
    /// <summary>
    /// Processes quiz answers and returns eligible products scoring >= 50%, ranked deterministically.
    /// </summary>
    Task<FragranceRecommendationResponse> GetRecommendationsAsync(FragranceRecommendationRequest request, CancellationToken cancellationToken = default);
}
