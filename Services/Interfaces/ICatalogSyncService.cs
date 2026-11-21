using Stockify.DTOs.CatalogSync;

namespace Stockify.Services.Interfaces;

public interface ICatalogSyncService
{
    Task<SyncPushResponseDto> PushLocalOutboxAsync(CancellationToken cancellationToken = default);
    Task<ChangeFeedResponseDto> PullLiveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> ResolveConflictAsync(Guid conflictId, string winner, string? notes, CancellationToken cancellationToken = default);
    Task<SyncDashboardViewModel> GetDashboardViewModelAsync(CancellationToken cancellationToken = default);
    Task ToggleSyncPauseAsync(bool pause);
    Task RetryFailedEventsAsync();
    Task<string?> UploadMediaToLiveAsync(string localRelativePath, CancellationToken cancellationToken = default);
}
