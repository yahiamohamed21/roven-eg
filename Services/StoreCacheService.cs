using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

/// <summary>
/// Centralized implementation of IStoreCacheService managing in-memory cache lifecycle for the ROVEN storefront.
/// </summary>
public class StoreCacheService : IStoreCacheService
{
    public const string AllProductsCacheKey = "Roven_Store_AllProducts";
    public const string CategoriesCacheKey = "Roven_Store_Categories";
    public const string HomeSettingsCacheKey = "Roven_Store_HomeSettings";
    public const string HomePageViewModelCacheKey = "Roven_Store_HomePageViewModel";

    private readonly IMemoryCache _cache;
    private readonly ILogger<StoreCacheService> _logger;

    public StoreCacheService(IMemoryCache cache, ILogger<StoreCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void InvalidateCatalogCache()
    {
        _cache.Remove(AllProductsCacheKey);
        _logger.LogInformation("Store catalog cache invalidated: key '{Key}' removed.", AllProductsCacheKey);
    }

    public void InvalidateCategoriesCache()
    {
        _cache.Remove(CategoriesCacheKey);
        _logger.LogInformation("Store categories cache invalidated: key '{Key}' removed.", CategoriesCacheKey);
    }

    public void InvalidateHomeSettingsCache()
    {
        _cache.Remove(HomeSettingsCacheKey);
        _logger.LogInformation("Store home settings cache invalidated: key '{Key}' removed.", HomeSettingsCacheKey);
    }

    public void InvalidateHomePageCache()
    {
        _cache.Remove(HomePageViewModelCacheKey);
        _logger.LogInformation("Store home page view model cache invalidated: key '{Key}' removed.", HomePageViewModelCacheKey);
    }

    public void InvalidateAll()
    {
        _cache.Remove(AllProductsCacheKey);
        _cache.Remove(CategoriesCacheKey);
        _cache.Remove(HomeSettingsCacheKey);
        _cache.Remove(HomePageViewModelCacheKey);
        _logger.LogInformation("All store caches invalidated: '{Key1}', '{Key2}', '{Key3}', '{Key4}' removed.",
            AllProductsCacheKey, CategoriesCacheKey, HomeSettingsCacheKey, HomePageViewModelCacheKey);
    }

    public void InvalidateStoreCache()
    {
        InvalidateAll();
    }
}
