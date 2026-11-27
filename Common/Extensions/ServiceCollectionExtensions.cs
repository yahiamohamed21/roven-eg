using Microsoft.Extensions.DependencyInjection;
using Stockify.Services.Implementations;
using Stockify.Services.Interfaces;

namespace Stockify.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IImageStorageService, CloudinaryImageStorageService>();
        services.AddScoped<IStoreCacheService, StoreCacheService>();
        services.AddScoped<IFragranceRecommendationService, FragranceRecommendationService>();
        services.AddSingleton<IMediaDeletionQueue, MediaDeletionQueue>();
        services.AddHostedService<MediaDeletionBackgroundService>();

        // Phase 7: Permanent Bidirectional Catalog Synchronization
        services.AddHttpClient();
        services.AddScoped<ICatalogSyncService, CatalogSyncService>();
        services.AddHostedService<CatalogSyncBackgroundService>();

        // Web Push Notifications (PWA / iOS Home Screen)
        services.AddScoped<IPushNotificationService, WebPushNotificationService>();
        services.AddHostedService<AbandonedCartPushBackgroundService>();

        return services;
    }
}

