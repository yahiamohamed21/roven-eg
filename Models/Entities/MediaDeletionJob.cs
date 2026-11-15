using System;

namespace Stockify.Models.Entities;

/// <summary>
/// Status of an asynchronous media deletion job.
/// </summary>
public enum MediaDeletionJobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>
/// Persistent outbox job entity for asynchronous media asset deletions.
/// Guarantees that deletion jobs survive application restarts, server crashes,
/// and deployments without loss, while supporting distributed concurrency locks
/// and exponential backoff retry policies.
/// </summary>
public class MediaDeletionJob
{
    public int Id { get; set; }

    /// <summary>
    /// The full remote URL of the asset to be deleted (e.g. Cloudinary secure URL).
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional extracted remote asset identifier / public ID.
    /// </summary>
    public string? PublicId { get; set; }

    /// <summary>
    /// Current execution state of the job.
    /// </summary>
    public MediaDeletionJobStatus Status { get; set; } = MediaDeletionJobStatus.Pending;

    /// <summary>
    /// Current number of retry attempts made for transient failures.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum permitted retry attempts before transitioning to Failed status.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Last error or diagnostic message recorded during processing.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// UTC timestamp when the deletion job was initially enqueued.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the job reached Completed or Failed state.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Earliest UTC timestamp at which the next retry attempt may execute.
    /// </summary>
    public DateTime? NextRetryUtc { get; set; }

    /// <summary>
    /// Unique identifier of the worker or process instance holding the execution lease.
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// UTC timestamp when the execution lease was acquired.
    /// </summary>
    public DateTime? LockedAtUtc { get; set; }
}
