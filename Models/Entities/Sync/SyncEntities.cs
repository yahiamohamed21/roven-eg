using System.ComponentModel.DataAnnotations;

namespace Stockify.Models.Entities.Sync;

public enum SyncEventState
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Conflict = 4
}

public enum ConflictStatus
{
    Pending = 0,
    Resolved = 1
}

public class SyncOutboxEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty; // "Product", "Category"

    [Required]
    [MaxLength(100)]
    public string EntitySyncKey { get; set; } = string.Empty; // SKU for products, Name for categories

    [Required]
    [MaxLength(30)]
    public string Operation { get; set; } = string.Empty; // "Create", "Update", "Delete"

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    public SyncEventState State { get; set; } = SyncEventState.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }

    public int RetryCount { get; set; } = 0;

    public string? ErrorMessage { get; set; }

    public int EntityVersion { get; set; } = 1;

    [MaxLength(64)]
    public string? ContentHash { get; set; } // SHA-256 of payload to deduplicate
}

public class SyncConflict
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntitySyncKey { get; set; } = string.Empty;

    [Required]
    public string LocalPayloadJson { get; set; } = string.Empty;

    [Required]
    public string LivePayloadJson { get; set; } = string.Empty;

    [Required]
    public string ConflictingFields { get; set; } = string.Empty; // JSON array of field names e.g. ["Price", "Name"]

    public ConflictStatus Status { get; set; } = ConflictStatus.Pending;

    [MaxLength(20)]
    public string? ChosenWinner { get; set; } // "Local", "Live", "Merged"

    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    [MaxLength(500)]
    public string? ResolutionNotes { get; set; }
}

public class SyncStateRecord
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SyncHistoryLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(30)]
    public string Direction { get; set; } = string.Empty; // "LocalToLive", "LiveToLocal", "Reconciliation"

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty; // "Success", "Warning", "Error"

    public int ItemsProcessed { get; set; } = 0;

    public int ConflictsDetected { get; set; } = 0;

    public string Details { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class SyncChangeFeedEvent
{
    [Key]
    public long Cursor { get; set; } // Monotonic auto-incrementing identity for change feed

    [Required]
    public Guid EventId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntitySyncKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Operation { get; set; } = string.Empty;

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    public int EntityVersion { get; set; } = 1;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
