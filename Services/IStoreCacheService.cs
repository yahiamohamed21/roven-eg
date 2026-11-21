namespace Stockify.Services.Interfaces;

/// <summary>
/// Centralized cache management service for the ROVEN storefront and catalog.
/// Coordinates in-memory cache invalidation for catalog products, categories, and settings.
/// </summary>
public interface IStoreCacheService
{
    /// <summary>
    /// Invalidates the published product catalog cache ("Roven_Store_AllProducts").
    /// Must be invoked whenever stock quantities, prices, visibility, or product details change
    /// (e.g. after customer checkout, order cancellation restock, inventory adjustments, POS sales, or product edits).
    /// </summary>
    void InvalidateCatalogCache();

    /// <summary>
    /// Invalidates the storefront category listing cache ("Roven_Store_Categories").
    /// Must be invoked whenever categories are created, updated, or deleted.
    /// </summary>
    void InvalidateCategoriesCache();

    /// <summary>
    /// Invalidates the cached home and store settings ("Roven_Store_HomeSettings").
    /// </summary>
    void InvalidateHomeSettingsCache();

    /// <summary>
    /// Invalidates the cached storefront home page view model ("Roven_Store_HomePageViewModel").
    /// Must be invoked whenever featured products, deals, banners, or homepage settings change.
    /// </summary>
    void InvalidateHomePageCache();

    /// <summary>
    /// Atomically clears all store catalog, category, and settings caches.
    /// Replaces duplicate InvalidateStoreCache() private methods across controllers.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Backward-compatible alias for InvalidateAll().
    /// </summary>
    void InvalidateStoreCache();
}
