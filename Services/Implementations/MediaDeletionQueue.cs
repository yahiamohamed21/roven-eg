using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

/// <summary>
/// Hybrid Media Deletion Queue providing persistent database Outbox operations
/// when the schema table exists, with automatic seamless in-memory channel fallback.
/// </summary>
public class MediaDeletionQueue : IMediaDeletionQueue
{
    private readonly Channel<string> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaDeletionQueue> _logger;

    private bool? _cachedTableAvailability;
    private DateTime _lastTableCheckTime = DateTime.MinValue;
    private static readonly TimeSpan TableCheckInterval = TimeSpan.FromMinutes(1);

    public MediaDeletionQueue(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaDeletionQueue> logger,
        int memoryCapacity = 1000)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var options = new BoundedChannelOptions(memoryCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<string>(options);
    }

    public async Task<bool> IsOutboxTableAvailableAsync()
    {
        if (_cachedTableAvailability.HasValue && (DateTime.UtcNow - _lastTableCheckTime) < TableCheckInterval)
        {
            return _cachedTableAvailability.Value;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (context.Database.ProviderName != null && context.Database.ProviderName.Contains("InMemory"))
            {
                _cachedTableAvailability = true;
            }
            else
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MediaDeletionJobs'";
                var countObj = await cmd.ExecuteScalarAsync();
                int count = Convert.ToInt32(countObj);
                _cachedTableAvailability = count > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query INFORMATION_SCHEMA.TABLES for MediaDeletionJobs");
            _cachedTableAvailability = false;
        }

        _lastTableCheckTime = DateTime.UtcNow;
        return _cachedTableAvailability.Value;
    }

    public async ValueTask EnqueueAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        bool outboxAvailable = await IsOutboxTableAvailableAsync();

        if (outboxAvailable)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Duplicate prevention: check if an active (Pending or Processing) job already exists for this URL
                bool alreadyActive = await context.MediaDeletionJobs.AnyAsync(j =>
                    j.ImageUrl == imageUrl &&
                    (j.Status == MediaDeletionJobStatus.Pending || j.Status == MediaDeletionJobStatus.Processing));

                if (alreadyActive)
                {
                    _logger.LogInformation("Deletion job for asset is already pending or processing; skipping duplicate: {Url}", imageUrl);
                    return;
                }

                var job = new MediaDeletionJob
                {
                    ImageUrl = imageUrl,
                    Status = MediaDeletionJobStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    MaxRetries = 5,
                    RetryCount = 0
                };

                context.MediaDeletionJobs.Add(job);
                await context.SaveChangesAsync();

                _logger.LogInformation("Persisted media deletion job to Outbox (Job #{Id}) for: {Url}", job.Id, imageUrl);

                // Notify in-process channel to immediately trigger background worker loop
                _channel.Writer.TryWrite(imageUrl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist deletion job to database Outbox. Falling back to in-memory channel for: {Url}", imageUrl);
            }
        }
        else
        {
            _logger.LogDebug("Persistent Outbox table not yet migrated; enqueuing to in-memory channel: {Url}", imageUrl);
        }

        // In-memory fallback
        await _channel.Writer.WriteAsync(imageUrl);
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public async Task<int> ProcessOutboxBatchAsync(CancellationToken cancellationToken)
    {
        bool outboxAvailable = await IsOutboxTableAvailableAsync();
        if (!outboxAvailable)
        {
            return 0;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IImageStorageService>();

        var workerId = $"worker-{Guid.NewGuid():N}"[..18];
        var now = DateTime.UtcNow;
        var staleThreshold = now.AddMinutes(-5); // 5-minute lease expiration

        // Recover expired locks and select eligible pending or retry-ready jobs
        var eligibleJobs = await context.MediaDeletionJobs
            .Where(j => j.Status == MediaDeletionJobStatus.Pending
                     || (j.Status == MediaDeletionJobStatus.Processing && j.LockedAtUtc < staleThreshold)
                     || (j.Status == MediaDeletionJobStatus.Failed && j.RetryCount < j.MaxRetries && j.NextRetryUtc <= now))
            .OrderBy(j => j.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (!eligibleJobs.Any())
        {
            return 0;
        }

        // Concurrency lock: acquire lease on selected batch
        foreach (var job in eligibleJobs)
        {
            job.Status = MediaDeletionJobStatus.Processing;
            job.LockedBy = workerId;
            job.LockedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        int processedCount = 0;

        foreach (var job in eligibleJobs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // 1. Live TOCTOU Reference Verification: ensure asset is unreferenced
                bool isReferenced = await context.Products.AnyAsync(p => p.ImageUrl == job.ImageUrl, cancellationToken)
                    || await context.HeroBanners.AnyAsync(b => b.DesktopImageUrl == job.ImageUrl || b.MobileImageUrl == job.ImageUrl, cancellationToken);

                if (isReferenced)
                {
                    _logger.LogInformation("Outbox Job #{Id}: Asset {Url} is currently referenced in database; aborting deletion to protect active content.", job.Id, job.ImageUrl);
                    job.Status = MediaDeletionJobStatus.Completed;
                    job.LastError = "Asset is referenced in database; deletion aborted to protect active media.";
                    job.ProcessedAt = DateTime.UtcNow;
                    job.LockedBy = null;
                    job.LockedAtUtc = null;
                    processedCount++;
                    continue;
                }

                // 2. Execute remote Cloudinary deletion
                if (!job.ImageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Outbox Job #{Id}: Asset is not Cloudinary; completing without remote deletion: {Url}", job.Id, job.ImageUrl);
                    job.Status = MediaDeletionJobStatus.Completed;
                    job.ProcessedAt = DateTime.UtcNow;
                    job.LockedBy = null;
                    job.LockedAtUtc = null;
                    processedCount++;
                    continue;
                }

                bool deleted = await storageService.DeleteImageAsync(job.ImageUrl);

                if (deleted)
                {
                    _logger.LogInformation("Outbox Job #{Id}: Successfully deleted Cloudinary asset: {Url}", job.Id, job.ImageUrl);
                    job.Status = MediaDeletionJobStatus.Completed;
                    job.ProcessedAt = DateTime.UtcNow;
                    job.LastError = null;
                    job.LockedBy = null;
                    job.LockedAtUtc = null;
                    processedCount++;
                }
                else
                {
                    job.RetryCount++;
                    job.LastError = $"Cloudinary returned false on attempt {job.RetryCount}/{job.MaxRetries}";
                    job.LockedBy = null;
                    job.LockedAtUtc = null;

                    if (job.RetryCount >= job.MaxRetries)
                    {
                        job.Status = MediaDeletionJobStatus.Failed;
                        job.NextRetryUtc = null;
                        _logger.LogError("Outbox Job #{Id}: Permanently failed after {Retries} attempts: {Url}", job.Id, job.RetryCount, job.ImageUrl);
                    }
                    else
                    {
                        job.Status = MediaDeletionJobStatus.Pending;
                        job.NextRetryUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, job.RetryCount));
                        _logger.LogWarning("Outbox Job #{Id}: Transient deletion failure. Scheduled retry {Retry} at {Time}", job.Id, job.RetryCount, job.NextRetryUtc);
                    }
                }
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message;
                job.LockedBy = null;
                job.LockedAtUtc = null;

                if (job.RetryCount >= job.MaxRetries)
                {
                    job.Status = MediaDeletionJobStatus.Failed;
                    job.NextRetryUtc = null;
                    _logger.LogError(ex, "Outbox Job #{Id}: Exception during deletion after {Retries} attempts: {Url}", job.Id, job.RetryCount, job.ImageUrl);
                }
                else
                {
                    job.Status = MediaDeletionJobStatus.Pending;
                    job.NextRetryUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, job.RetryCount));
                    _logger.LogWarning(ex, "Outbox Job #{Id}: Exception during deletion. Scheduled retry {Retry} at {Time}", job.Id, job.RetryCount, job.NextRetryUtc);
                }
            }
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return processedCount;
    }
}
