using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

/// <summary>
/// Hosted background service managing asynchronous media deletions.
/// Dispatches persistent Outbox jobs across application restarts with distributed locking,
/// while concurrently serving in-memory fallback channels with live TOCTOU safeguards.
/// </summary>
public class MediaDeletionBackgroundService : BackgroundService
{
    private readonly IMediaDeletionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaDeletionBackgroundService> _logger;

    public MediaDeletionBackgroundService(
        IMediaDeletionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MediaDeletionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MediaDeletionBackgroundService is running.");

        var outboxTask = ProcessOutboxLoopAsync(stoppingToken);
        var memoryChannelTask = ProcessMemoryChannelLoopAsync(stoppingToken);

        await Task.WhenAll(outboxTask, memoryChannelTask);

        _logger.LogInformation("MediaDeletionBackgroundService stopped gracefully.");
    }

    /// <summary>
    /// Periodic loop that scans the database Outbox for pending, retry-due, or orphaned jobs.
    /// Resumes work automatically across application restarts.
    /// </summary>
    private async Task ProcessOutboxLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed = await _queue.ProcessOutboxBatchAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogDebug("Processed {Count} persistent Outbox deletion jobs.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing persistent Outbox deletion batch.");
            }

            try
            {
                // Poll Outbox every 5 seconds
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Reads and processes items from the in-memory channel (used as fallback when DB Outbox is not yet migrated).
    /// </summary>
    private async Task ProcessMemoryChannelLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var imageUrl in _queue.ReadAllAsync(stoppingToken))
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    continue;
                }

                // If Outbox is available, it handles the deletion through the Outbox table
                bool outboxAvailable = await _queue.IsOutboxTableAvailableAsync();
                if (outboxAvailable)
                {
                    // Trigger an immediate outbox pass
                    await _queue.ProcessOutboxBatchAsync(stoppingToken);
                    continue;
                }

                if (!imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Skipping deletion of non-Cloudinary asset: {Url}", imageUrl);
                    continue;
                }

                try
                {
                    await ProcessSingleMemoryDeletionAsync(imageUrl, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in memory deletion fallback for: {Url}", imageUrl);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in memory channel processor loop.");
        }
    }

    private async Task ProcessSingleMemoryDeletionAsync(string imageUrl, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IImageStorageService>();

        // 1. Live TOCTOU verification
        bool isReferenced = await context.Products.AnyAsync(p => p.ImageUrl == imageUrl, stoppingToken)
            || await context.HeroBanners.AnyAsync(b => b.DesktopImageUrl == imageUrl || b.MobileImageUrl == imageUrl, stoppingToken);

        if (isReferenced)
        {
            _logger.LogInformation("Asset is referenced in database; aborting memory fallback deletion: {Url}", imageUrl);
            return;
        }

        // 2. Retry loop with exponential backoff
        const int maxAttempts = 3;
        bool deleted = false;

        for (int attempt = 1; attempt <= maxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                deleted = await storageService.DeleteImageAsync(imageUrl);
                if (deleted)
                {
                    _logger.LogInformation("Successfully deleted Cloudinary asset via memory channel: {Url}", imageUrl);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory deletion attempt {Attempt}/{MaxAttempts} failed for {Url}", attempt, maxAttempts, imageUrl);
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (!deleted && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError("Memory deletion failed after {MaxAttempts} attempts for: {Url}", maxAttempts, imageUrl);
        }
    }
}
