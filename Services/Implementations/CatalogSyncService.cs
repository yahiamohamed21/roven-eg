using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.DTOs.CatalogSync;
using Stockify.Models.Entities;
using Stockify.Models.Entities.Sync;
using Stockify.Services.Interfaces;
using Stockify.Services.Sync;

namespace Stockify.Services.Implementations;

public class CatalogSyncService : ICatalogSyncService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CatalogSyncService> _logger;
    private readonly IWebHostEnvironment _environment;

    public CatalogSyncService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CatalogSyncService> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
    }

    private string GetLiveBaseUrl()
    {
        return (_configuration["CatalogSync:LiveBaseUrl"] ?? "https://roven-eg.online").TrimEnd('/');
    }

    private string GetApiKey()
    {
        return _configuration["CatalogSync:ApiKey"] ?? string.Empty;
    }

    private HttpClient CreateSyncHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(GetLiveBaseUrl());
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("X-Sync-Api-Key", GetApiKey());
        return client;
    }

    private async Task<bool> IsSyncPausedAsync()
    {
        var record = await _context.SyncStateRecords.FirstOrDefaultAsync(r => r.Key == "SyncPaused");
        return record != null && string.Equals(record.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SyncPushResponseDto> PushLocalOutboxAsync(CancellationToken cancellationToken = default)
    {
        if (await IsSyncPausedAsync())
        {
            return new SyncPushResponseDto { Success = true, Message = "Synchronization is currently paused." };
        }

        var pendingEvents = await _context.SyncOutboxEvents
            .Where(e => e.State == SyncEventState.Pending || (e.State == SyncEventState.Failed && e.RetryCount < 5))
            .OrderBy(e => e.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingEvents.Count == 0)
        {
            return new SyncPushResponseDto { Success = true, Message = "No pending outbox events to push." };
        }

        foreach (var evt in pendingEvents)
        {
            evt.State = SyncEventState.Processing;
        }
        await _context.SaveChangesAsync(cancellationToken);

        var pushRequest = new SyncPushRequestDto
        {
            RequestTimestampUtc = DateTime.UtcNow,
            Events = pendingEvents.Select(e => new SyncItemEventDto
            {
                EventId = e.Id,
                EntityType = e.EntityType,
                EntitySyncKey = e.EntitySyncKey,
                Operation = e.Operation,
                PayloadJson = e.PayloadJson,
                EntityVersion = e.EntityVersion,
                TimestampUtc = e.CreatedAtUtc
            }).ToList()
        };

        try
        {
            var client = CreateSyncHttpClient();
            var jsonContent = new StringContent(JsonSerializer.Serialize(pushRequest), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/sync/push", jsonContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Sync push returned status {Status}: {Body}", response.StatusCode, errorBody);

                foreach (var evt in pendingEvents)
                {
                    evt.State = SyncEventState.Failed;
                    evt.RetryCount += 1;
                    evt.ErrorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}";
                }
                await _context.SaveChangesAsync(cancellationToken);

                await RecordHistoryLogAsync("LocalToLive", "Error", pendingEvents.Count, 0, $"HTTP {(int)response.StatusCode}: {errorBody}");

                return new SyncPushResponseDto
                {
                    Success = false,
                    Message = $"Push failed with HTTP {(int)response.StatusCode}: {errorBody}"
                };
            }

            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var resultDto = await JsonSerializer.DeserializeAsync<SyncPushResponseDto>(responseStream, cancellationToken: cancellationToken)
                ?? new SyncPushResponseDto { Success = false, Message = "Empty response from sync server." };

            int successCount = 0;
            int failedCount = 0;
            int conflictCount = 0;

            foreach (var evt in pendingEvents)
            {
                var itemResult = resultDto.Results.FirstOrDefault(r => r.EventId == evt.Id || r.EntitySyncKey == evt.EntitySyncKey);
                if (itemResult != null && itemResult.Success)
                {
                    evt.State = SyncEventState.Completed;
                    evt.ProcessedAtUtc = DateTime.UtcNow;
                    evt.ErrorMessage = null;
                    successCount++;
                }
                else if (itemResult != null && itemResult.ConflictDetected)
                {
                    evt.State = SyncEventState.Conflict;
                    evt.ErrorMessage = "Conflict detected with live server version.";
                    conflictCount++;
                }
                else
                {
                    evt.State = SyncEventState.Failed;
                    evt.RetryCount += 1;
                    evt.ErrorMessage = itemResult?.Error ?? "Unknown error during push processing.";
                    failedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            string status = failedCount == 0 && conflictCount == 0 ? "Success" : "Warning";
            await RecordHistoryLogAsync("LocalToLive", status, pendingEvents.Count, conflictCount,
                $"Pushed {pendingEvents.Count} items. Success: {successCount}, Failed: {failedCount}, Conflicts: {conflictCount}");

            return resultDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during catalog sync push");
            foreach (var evt in pendingEvents)
            {
                evt.State = SyncEventState.Failed;
                evt.RetryCount += 1;
                evt.ErrorMessage = ex.Message;
            }
            await _context.SaveChangesAsync(cancellationToken);

            await RecordHistoryLogAsync("LocalToLive", "Error", pendingEvents.Count, 0, ex.Message);

            return new SyncPushResponseDto { Success = false, Message = $"Network/system error: {ex.Message}" };
        }
    }

    public async Task<ChangeFeedResponseDto> PullLiveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (await IsSyncPausedAsync())
        {
            return new ChangeFeedResponseDto { Success = true, Message = "Synchronization is currently paused." };
        }

        // Get cursor
        var cursorRecord = await _context.SyncStateRecords.FirstOrDefaultAsync(r => r.Key == "LastLiveCursor", cancellationToken);
        long lastCursor = 0;
        if (cursorRecord != null && long.TryParse(cursorRecord.Value, out var parsedCursor))
        {
            lastCursor = parsedCursor;
        }

        try
        {
            var client = CreateSyncHttpClient();
            var response = await client.GetAsync($"/api/sync/change-feed?cursor={lastCursor}&limit=50", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Sync pull change-feed returned status {Status}: {Body}", response.StatusCode, errorBody);
                await RecordHistoryLogAsync("LiveToLocal", "Error", 0, 0, $"HTTP {(int)response.StatusCode}: {errorBody}");
                return new ChangeFeedResponseDto { Success = false, Message = $"Pull failed with HTTP {(int)response.StatusCode}" };
            }

            var feed = await response.Content.ReadFromJsonAsync<ChangeFeedResponseDto>(cancellationToken: cancellationToken);
            if (feed == null || feed.Changes == null || feed.Changes.Count == 0)
            {
                await UpdateSyncStateRecordAsync("LastSyncTimeUtc", DateTime.UtcNow.ToString("O"));
                return new ChangeFeedResponseDto { Success = true, Message = "No new changes on live server." };
            }

            int processedCount = 0;
            int conflictCount = 0;

            // Apply incoming changes locally with outbox suppression
            _context.SuppressSyncOutbox = true;
            SyncOutboxHelper.SuppressOutboxScope.Value = true;

            try
            {
                foreach (var change in feed.Changes)
                {
                    if (string.Equals(change.EntityType, "Product", StringComparison.OrdinalIgnoreCase))
                    {
                        var productDto = JsonSerializer.Deserialize<ProductSyncDto>(change.PayloadJson);
                        if (productDto == null || string.IsNullOrWhiteSpace(productDto.Sku)) continue;

                        var localProduct = await _context.Products
                            .Include(p => p.Category)
                            .FirstOrDefaultAsync(p => p.Sku == productDto.Sku, cancellationToken);

                        // Check for conflicting pending local changes
                        var pendingOutbox = await _context.SyncOutboxEvents
                            .FirstOrDefaultAsync(e => e.EntityType == "Product" && e.EntitySyncKey == productDto.Sku &&
                                                      (e.State == SyncEventState.Pending || e.State == SyncEventState.Processing), cancellationToken);

                        if (pendingOutbox != null && localProduct != null)
                        {
                            // Detect if fields actually conflict
                            var localDto = JsonSerializer.Deserialize<ProductSyncDto>(pendingOutbox.PayloadJson);
                            var conflictingFields = GetConflictingProductFields(localDto, productDto);

                            if (conflictingFields.Count > 0)
                            {
                                conflictCount++;
                                var conflict = new SyncConflict
                                {
                                    EntityType = "Product",
                                    EntitySyncKey = productDto.Sku,
                                    LocalPayloadJson = pendingOutbox.PayloadJson,
                                    LivePayloadJson = change.PayloadJson,
                                    ConflictingFields = JsonSerializer.Serialize(conflictingFields),
                                    Status = ConflictStatus.Pending,
                                    DetectedAtUtc = DateTime.UtcNow
                                };
                                _context.SyncConflicts.Add(conflict);
                                pendingOutbox.State = SyncEventState.Conflict;
                                continue;
                            }
                        }

                        // Apply change locally
                        if (string.Equals(change.Operation, "Delete", StringComparison.OrdinalIgnoreCase))
                        {
                            if (localProduct != null)
                            {
                                localProduct.IsActive = false;
                                localProduct.IsPublished = false;
                            }
                        }
                        else
                        {
                            Category? category = null;
                            if (!string.IsNullOrWhiteSpace(productDto.CategoryName))
                            {
                                category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == productDto.CategoryName, cancellationToken);
                                if (category == null)
                                {
                                    category = new Category
                                    {
                                        Name = productDto.CategoryName,
                                        Description = $"Category synced for SKU {productDto.Sku}"
                                    };
                                    _context.Categories.Add(category);
                                    await _context.SaveChangesAsync(cancellationToken);
                                }
                            }

                            if (localProduct == null)
                            {
                                var newProduct = new Product
                                {
                                    Sku = productDto.Sku,
                                    Name = productDto.Name,
                                    Description = productDto.Description,
                                    FragranceTagline = productDto.FragranceTagline,
                                    Price = productDto.Price,
                                    StockQuantity = productDto.StockQuantity,
                                    ReorderLevel = productDto.ReorderLevel,
                                    ImageUrl = productDto.ImageUrl,
                                    IsActive = productDto.IsActive,
                                    IsPublished = productDto.IsPublished,
                                    IsFeatured = productDto.IsFeatured,
                                    IsBestSeller = productDto.IsBestSeller,
                                    CategoryId = category?.Id ?? 2
                                };
                                _context.Products.Add(newProduct);
                            }
                            else
                            {
                                // Strictly preserve Find Your Scent fields!
                                localProduct.Name = productDto.Name;
                                localProduct.Description = productDto.Description;
                                localProduct.FragranceTagline = productDto.FragranceTagline;
                                localProduct.Price = productDto.Price;
                                localProduct.StockQuantity = productDto.StockQuantity;
                                localProduct.ReorderLevel = productDto.ReorderLevel;
                                if (!string.IsNullOrWhiteSpace(productDto.ImageUrl))
                                {
                                    localProduct.ImageUrl = productDto.ImageUrl;
                                }
                                localProduct.IsActive = productDto.IsActive;
                                localProduct.IsPublished = productDto.IsPublished;
                                localProduct.IsFeatured = productDto.IsFeatured;
                                localProduct.IsBestSeller = productDto.IsBestSeller;
                                if (category != null)
                                {
                                    localProduct.CategoryId = category.Id;
                                }
                            }
                        }

                        processedCount++;
                    }
                    else if (string.Equals(change.EntityType, "Category", StringComparison.OrdinalIgnoreCase))
                    {
                        var categoryDto = JsonSerializer.Deserialize<CategorySyncDto>(change.PayloadJson);
                        if (categoryDto == null || string.IsNullOrWhiteSpace(categoryDto.Name)) continue;

                        var localCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == categoryDto.Name, cancellationToken);
                        if (localCategory == null)
                        {
                            var newCat = new Category
                            {
                                Name = categoryDto.Name,
                                Description = categoryDto.Description,
                                ImageUrl = categoryDto.ImageUrl
                            };
                            _context.Categories.Add(newCat);
                        }
                        else
                        {
                            localCategory.Description = categoryDto.Description;
                            if (!string.IsNullOrWhiteSpace(categoryDto.ImageUrl))
                            {
                                localCategory.ImageUrl = categoryDto.ImageUrl;
                            }
                        }
                        processedCount++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _context.SuppressSyncOutbox = false;
                SyncOutboxHelper.SuppressOutboxScope.Value = false;
            }

            // Update Cursor and LastSyncTime
            await UpdateSyncStateRecordAsync("LastLiveCursor", feed.CurrentCursor.ToString());
            await UpdateSyncStateRecordAsync("LastSyncTimeUtc", DateTime.UtcNow.ToString("O"));

            string logStatus = conflictCount > 0 ? "Warning" : "Success";
            await RecordHistoryLogAsync("LiveToLocal", logStatus, processedCount, conflictCount,
                $"Pulled {feed.Changes.Count} changes from live. Processed: {processedCount}, Conflicts: {conflictCount}. New Cursor: {feed.CurrentCursor}");

            return feed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during catalog sync pull");
            await RecordHistoryLogAsync("LiveToLocal", "Error", 0, 0, ex.Message);
            return new ChangeFeedResponseDto { Success = false, Message = $"Pull error: {ex.Message}" };
        }
    }

    private List<string> GetConflictingProductFields(ProductSyncDto? local, ProductSyncDto? live)
    {
        var conflicts = new List<string>();
        if (local == null || live == null) return conflicts;

        if (local.Name != live.Name) conflicts.Add(nameof(local.Name));
        if (local.Price != live.Price) conflicts.Add(nameof(local.Price));
        if (local.StockQuantity != live.StockQuantity) conflicts.Add(nameof(local.StockQuantity));
        if (local.Description != live.Description) conflicts.Add(nameof(local.Description));
        if (local.CategoryName != live.CategoryName) conflicts.Add(nameof(local.CategoryName));
        if (local.IsActive != live.IsActive) conflicts.Add(nameof(local.IsActive));
        if (local.IsPublished != live.IsPublished) conflicts.Add(nameof(local.IsPublished));

        return conflicts;
    }

    public async Task<bool> ResolveConflictAsync(Guid conflictId, string winner, string? notes, CancellationToken cancellationToken = default)
    {
        var conflict = await _context.SyncConflicts.FirstOrDefaultAsync(c => c.Id == conflictId, cancellationToken);
        if (conflict == null) return false;

        conflict.Status = ConflictStatus.Resolved;
        conflict.ChosenWinner = winner;
        conflict.ResolvedAtUtc = DateTime.UtcNow;
        conflict.ResolutionNotes = notes;

        if (string.Equals(winner, "Local", StringComparison.OrdinalIgnoreCase))
        {
            // Push local version to live by creating a high-priority outbox event
            var localDto = JsonSerializer.Deserialize<ProductSyncDto>(conflict.LocalPayloadJson);
            if (localDto != null)
            {
                var outbox = new SyncOutboxEvent
                {
                    EntityType = conflict.EntityType,
                    EntitySyncKey = conflict.EntitySyncKey,
                    Operation = "Update",
                    PayloadJson = conflict.LocalPayloadJson,
                    State = SyncEventState.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    ContentHash = SyncOutboxHelper.ComputeStringHash(conflict.LocalPayloadJson)
                };
                _context.SyncOutboxEvents.Add(outbox);
            }
        }
        else if (string.Equals(winner, "Live", StringComparison.OrdinalIgnoreCase))
        {
            // Apply live version locally with outbox suppression
            _context.SuppressSyncOutbox = true;
            SyncOutboxHelper.SuppressOutboxScope.Value = true;
            try
            {
                if (string.Equals(conflict.EntityType, "Product", StringComparison.OrdinalIgnoreCase))
                {
                    var liveDto = JsonSerializer.Deserialize<ProductSyncDto>(conflict.LivePayloadJson);
                    if (liveDto != null)
                    {
                        var localProduct = await _context.Products.FirstOrDefaultAsync(p => p.Sku == liveDto.Sku, cancellationToken);
                        if (localProduct != null)
                        {
                            localProduct.Name = liveDto.Name;
                            localProduct.Price = liveDto.Price;
                            localProduct.StockQuantity = liveDto.StockQuantity;
                            localProduct.Description = liveDto.Description;
                            localProduct.FragranceTagline = liveDto.FragranceTagline;
                            localProduct.IsActive = liveDto.IsActive;
                            localProduct.IsPublished = liveDto.IsPublished;
                            localProduct.IsFeatured = liveDto.IsFeatured;
                            localProduct.IsBestSeller = liveDto.IsBestSeller;
                        }
                    }
                }

                // Remove or complete pending conflicting outbox event
                var pendingOutbox = await _context.SyncOutboxEvents
                    .Where(e => e.EntityType == conflict.EntityType && e.EntitySyncKey == conflict.EntitySyncKey && e.State == SyncEventState.Conflict)
                    .ToListAsync(cancellationToken);

                foreach (var evt in pendingOutbox)
                {
                    evt.State = SyncEventState.Completed;
                    evt.ProcessedAtUtc = DateTime.UtcNow;
                    evt.ErrorMessage = "Resolved by accepting Live version.";
                }
            }
            finally
            {
                _context.SuppressSyncOutbox = false;
                SyncOutboxHelper.SuppressOutboxScope.Value = false;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await RecordHistoryLogAsync("Resolution", "Success", 1, 0, $"Resolved conflict for {conflict.EntityType} {conflict.EntitySyncKey} in favor of {winner}.");

        return true;
    }

    public async Task<SyncDashboardViewModel> GetDashboardViewModelAsync(CancellationToken cancellationToken = default)
    {
        var isPaused = await IsSyncPausedAsync();
        var isEnabled = _configuration.GetValue<bool>("CatalogSync:Enabled", true);

        var cursorRecord = await _context.SyncStateRecords.FirstOrDefaultAsync(r => r.Key == "LastLiveCursor", cancellationToken);
        long lastCursor = (cursorRecord != null && long.TryParse(cursorRecord.Value, out var c)) ? c : 0;

        var lastSyncRecord = await _context.SyncStateRecords.FirstOrDefaultAsync(r => r.Key == "LastSyncTimeUtc", cancellationToken);
        DateTime? lastSyncTime = (lastSyncRecord != null && DateTime.TryParse(lastSyncRecord.Value, out var dt)) ? dt : null;

        var pendingCount = await _context.SyncOutboxEvents.CountAsync(e => e.State == SyncEventState.Pending || e.State == SyncEventState.Processing, cancellationToken);
        var failedCount = await _context.SyncOutboxEvents.CountAsync(e => e.State == SyncEventState.Failed, cancellationToken);
        var conflictCount = await _context.SyncConflicts.CountAsync(c => c.Status == ConflictStatus.Pending, cancellationToken);

        var totalProducts = await _context.Products.CountAsync(cancellationToken);
        var totalCategories = await _context.Categories.CountAsync(cancellationToken);

        var recentOutbox = await _context.SyncOutboxEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(15)
            .ToListAsync(cancellationToken);

        var pendingConflicts = await _context.SyncConflicts
            .Where(c => c.Status == ConflictStatus.Pending)
            .OrderByDescending(c => c.DetectedAtUtc)
            .ToListAsync(cancellationToken);

        var recentLogs = await _context.SyncHistoryLogs
            .OrderByDescending(l => l.TimestampUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new SyncDashboardViewModel
        {
            IsSyncEnabled = isEnabled,
            IsSyncPaused = isPaused,
            LiveBaseUrl = GetLiveBaseUrl(),
            LastSyncTimeUtc = lastSyncTime,
            LastLiveCursor = lastCursor,
            PendingOutboxCount = pendingCount,
            FailedOutboxCount = failedCount,
            ActiveConflictsCount = conflictCount,
            TotalLocalProducts = totalProducts,
            TotalLocalCategories = totalCategories,
            RecentOutboxEvents = recentOutbox,
            PendingConflicts = pendingConflicts,
            RecentLogs = recentLogs
        };
    }

    public async Task ToggleSyncPauseAsync(bool pause)
    {
        await UpdateSyncStateRecordAsync("SyncPaused", pause ? "true" : "false");
        await RecordHistoryLogAsync("Control", "Success", 0, 0, $"Synchronization was {(pause ? "PAUSED" : "RESUMED")} by Administrator.");
    }

    public async Task RetryFailedEventsAsync()
    {
        var failed = await _context.SyncOutboxEvents
            .Where(e => e.State == SyncEventState.Failed)
            .ToListAsync();

        foreach (var evt in failed)
        {
            evt.State = SyncEventState.Pending;
            evt.RetryCount = 0;
            evt.ErrorMessage = "Manually queued for retry by Administrator.";
        }

        await _context.SaveChangesAsync();
        await RecordHistoryLogAsync("Control", "Success", failed.Count, 0, $"Reset {failed.Count} failed events to Pending status for retry.");
    }

    public async Task<string?> UploadMediaToLiveAsync(string localRelativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localRelativePath)) return null;
        if (localRelativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            localRelativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return localRelativePath; // Cloudinary URL
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(webRoot, localRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath)) return null;

        try
        {
            var client = CreateSyncHttpClient();
            using var form = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(fullPath);
            using var streamContent = new StreamContent(fileStream);

            string fileName = Path.GetFileName(fullPath);
            string contentType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
                                 fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/jpeg";

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(streamContent, "file", fileName);

            string folder = localRelativePath.Contains("categories", StringComparison.OrdinalIgnoreCase) ? "categories" : "products";
            form.Add(new StringContent(folder), "targetFolder");

            var response = await client.PostAsync("/api/sync/upload-media", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to upload media {Path} to live: {Error}", localRelativePath, error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MediaUploadResponseDto>(cancellationToken: cancellationToken);
            return result?.RelativeUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading media {Path} to live server", localRelativePath);
            return null;
        }
    }

    private async Task UpdateSyncStateRecordAsync(string key, string value)
    {
        var record = await _context.SyncStateRecords.FirstOrDefaultAsync(r => r.Key == key);
        if (record == null)
        {
            record = new SyncStateRecord { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow };
            _context.SyncStateRecords.Add(record);
        }
        else
        {
            record.Value = value;
            record.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    private async Task RecordHistoryLogAsync(string direction, string status, int itemsProcessed, int conflicts, string details)
    {
        try
        {
            var log = new SyncHistoryLog
            {
                Direction = direction,
                Status = status,
                ItemsProcessed = itemsProcessed,
                ConflictsDetected = conflicts,
                Details = details,
                TimestampUtc = DateTime.UtcNow
            };
            _context.SyncHistoryLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record sync history log");
        }
    }
}
