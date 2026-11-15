using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stockify.Services.Interfaces;

/// <summary>
/// Service contract for enqueuing and processing background media asset deletions.
/// Supports both persistent database Outbox jobs and in-memory fallback channels.
/// </summary>
public interface IMediaDeletionQueue
{
    /// <summary>
    /// Enqueues an asset URL to be safely deleted in the background.
    /// Persists to database Outbox if available, or in-memory channel.
    /// </summary>
    ValueTask EnqueueAsync(string imageUrl);

    /// <summary>
    /// Reads in-memory queued image URLs asynchronously.
    /// </summary>
    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the persistent database Outbox table is available.
    /// </summary>
    Task<bool> IsOutboxTableAvailableAsync();

    /// <summary>
    /// Processes a batch of pending or due persistent Outbox deletion jobs.
    /// </summary>
    Task<int> ProcessOutboxBatchAsync(CancellationToken cancellationToken);
}
