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

        return services;
    }
}

