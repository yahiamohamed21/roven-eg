using System.ComponentModel.DataAnnotations;
using Stockify.Models.Entities.Sync;

namespace Stockify.DTOs.CatalogSync;

public class ProductSyncDto
{
    [Required]
    public string Sku { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? FragranceTagline { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public int ReorderLevel { get; set; } = 10;

    public string? ImageUrl { get; set; }

    public string? ImageSha256 { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsPublished { get; set; } = true;

    public bool IsFeatured { get; set; } = false;

    public bool IsBestSeller { get; set; } = false;

    public int Version { get; set; } = 1;

    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CategorySyncDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string? ImageSha256 { get; set; }

    public int Version { get; set; } = 1;

    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SyncItemEventDto
{
    public Guid EventId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "Product", "Category"
    public string EntitySyncKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // "Create", "Update", "Delete"
    public string PayloadJson { get; set; } = string.Empty;
    public int EntityVersion { get; set; } = 1;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class SyncPushRequestDto
{
    public List<SyncItemEventDto> Events { get; set; } = new();
    public DateTime RequestTimestampUtc { get; set; } = DateTime.UtcNow;
}

public class SyncItemResultDto
{
    public Guid EventId { get; set; }
    public string EntitySyncKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool ConflictDetected { get; set; }
    public string? ConflictingPayloadJson { get; set; }
}

public class SyncPushResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SyncItemResultDto> Results { get; set; } = new();
    public long ServerCursor { get; set; }
}

public class ChangeFeedResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long CurrentCursor { get; set; }
    public bool HasMore { get; set; }
    public List<SyncItemEventDto> Changes { get; set; } = new();
}

public class MediaUploadResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? RelativeUrl { get; set; }
    public string? Sha256Hash { get; set; }
}

public class SyncDashboardViewModel
{
    public bool IsSyncEnabled { get; set; }
    public bool IsSyncPaused { get; set; }
    public string LiveBaseUrl { get; set; } = string.Empty;
    public DateTime? LastSyncTimeUtc { get; set; }
    public long LastLiveCursor { get; set; }
    public int PendingOutboxCount { get; set; }
    public int FailedOutboxCount { get; set; }
    public int ActiveConflictsCount { get; set; }
    public int TotalLocalProducts { get; set; }
    public int TotalLocalCategories { get; set; }
    public List<SyncOutboxEvent> RecentOutboxEvents { get; set; } = new();
    public List<SyncConflict> PendingConflicts { get; set; } = new();
    public List<SyncHistoryLog> RecentLogs { get; set; } = new();
}

public class ConflictResolutionRequestDto
{
    [Required]
    public Guid ConflictId { get; set; }

    [Required]
    [RegularExpression("^(Local|Live)$", ErrorMessage = "Winner must be either 'Local' or 'Live'.")]
    public string Winner { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
