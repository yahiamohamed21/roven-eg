using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class CatalogSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CatalogSyncBackgroundService> _logger;

    public CatalogSyncBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<CatalogSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // STRICT SAFETY CHECK:
        // Background sync MUST ONLY execute in Development environment and when explicitly enabled in configuration.
        // It MUST NEVER run on the live production server.
        bool isEnabled = _configuration.GetValue<bool>("CatalogSync:Enabled", false);
        bool isDevelopment = _environment.IsDevelopment();

        if (!isDevelopment || !isEnabled)
        {
            _logger.LogInformation("CatalogSyncBackgroundService is disabled (Development: {IsDev}, Enabled: {IsEnabled}).",
                isDevelopment, isEnabled);
            return;
        }

        int intervalSeconds = _configuration.GetValue<int>("CatalogSync:SyncIntervalSeconds", 300);
        if (intervalSeconds < 30) intervalSeconds = 30; // Safety floor

        _logger.LogInformation("CatalogSyncBackgroundService started. Interval: {Interval} seconds.", intervalSeconds);

        // Initial delay on startup to allow app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();

                _logger.LogDebug("Running automated catalog synchronization cycle...");

                // 1. Push pending local changes to Live
                var pushResult = await syncService.PushLocalOutboxAsync(stoppingToken);
                if (!pushResult.Success)
                {
                    _logger.LogWarning("Catalog sync push cycle completed with notice: {Message}", pushResult.Message);
                }

                // 2. Pull remote changes from Live to Local
                var pullResult = await syncService.PullLiveChangesAsync(stoppingToken);
                if (!pullResult.Success)
                {
                    _logger.LogWarning("Catalog sync pull cycle completed with notice: {Message}", pullResult.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("CatalogSyncBackgroundService shutting down gracefully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CatalogSyncBackgroundService execution cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
