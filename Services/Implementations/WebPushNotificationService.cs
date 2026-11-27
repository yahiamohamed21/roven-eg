using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Services.Interfaces;
using WebPush;
using PushSubscriptionEntity = Stockify.Models.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace Stockify.Services.Implementations;

public class WebPushNotificationService : IPushNotificationService
{
    private static readonly SemaphoreSlim VapidLock = new(1, 1);
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebPushNotificationService> _logger;

    public WebPushNotificationService(
        ApplicationDbContext context,
        ILogger<WebPushNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GetPublicVapidKeyAsync(CancellationToken ct = default)
    {
        var vapid = await EnsureVapidSettingsAsync(ct);
        return vapid.PublicKey;
    }

    public async Task SubscribeAsync(
        string endpoint,
        string p256dh,
        string auth,
        string? userId,
        string? userAgent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
            throw new ArgumentException("Invalid push subscription parameters.");

        var normalizedEndpoint = endpoint.Trim();
        var existing = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == normalizedEndpoint, ct);

        if (existing != null)
        {
            existing.P256dh = p256dh.Trim();
            existing.Auth = auth.Trim();
            existing.UserId = userId;
            existing.UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent;
            existing.LastSuccessAt = DateTime.UtcNow;
        }
        else
        {
            _context.PushSubscriptions.Add(new PushSubscriptionEntity
            {
                Endpoint = normalizedEndpoint,
                P256dh = p256dh.Trim(),
                Auth = auth.Trim(),
                UserId = userId,
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
                CreatedAt = DateTime.UtcNow,
                LastSuccessAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        var normalizedEndpoint = endpoint.Trim();
        var existing = await _context.PushSubscriptions
            .Where(s => s.Endpoint == normalizedEndpoint)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _context.PushSubscriptions.RemoveRange(existing);
            await _context.SaveChangesAsync(ct);
        }
    }

    public Task<int> GetActiveSubscriptionCountAsync(CancellationToken ct = default)
    {
        return _context.PushSubscriptions.AsNoTracking().CountAsync(ct);
    }

    public async Task<int> SendToAllAsync(
        string title,
        string body,
        string url = "/Dashboard",
        string? tag = null,
        CancellationToken ct = default)
    {
        var subscriptions = await _context.PushSubscriptions.ToListAsync(ct);
        if (subscriptions.Count == 0) return 0;

        var vapid = await EnsureVapidSettingsAsync(ct);
        var vapidDetails = new VapidDetails(vapid.Subject, vapid.PublicKey, vapid.PrivateKey);

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url = string.IsNullOrWhiteSpace(url) ? "/Dashboard" : url,
            tag = tag ?? "roven-alert",
            icon = "/images/pwa-icon-192.png",
            badge = "/images/favicon-64.png"
        });

        using var client = new WebPushClient();
        var deadSubscriptions = new List<PushSubscriptionEntity>();
        int sentCount = 0;

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new WebPushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapidDetails, ct);
                sub.LastSuccessAt = DateTime.UtcNow;
                sentCount++;
            }
            catch (WebPushException wpEx) when (wpEx.StatusCode == HttpStatusCode.Gone || wpEx.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Removing expired push subscription {Id} ({StatusCode}).", sub.Id, wpEx.StatusCode);
                deadSubscriptions.Add(sub);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed sending web push to subscription {Id}: {Message}", sub.Id, ex.Message);
            }
        }

        if (deadSubscriptions.Count > 0)
        {
            _context.PushSubscriptions.RemoveRange(deadSubscriptions);
        }

        if (sentCount > 0 || deadSubscriptions.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return sentCount;
    }

    public Task NotifyNewOrderAsync(int orderId, string orderNumber, string customerName, decimal totalAmount, CancellationToken ct = default)
    {
        var title = $"طلب جديد #{orderNumber}";
        var body = $"{customerName} — الإجمالي {totalAmount:N0} ج.م";
        return SendToAllAsync(title, body, $"/Orders/Details/{orderId}", $"order-{orderId}", ct);
    }

    public Task NotifyLowStockAsync(string productName, int remainingStock, int reorderLevel, CancellationToken ct = default)
    {
        var title = "تنبيه نفاد المخزون";
        var body = remainingStock <= 0
            ? $"المنتج «{productName}» نفد من المخزون بالكامل!"
            : $"المنتج «{productName}» أوشك على النفاد (المتبقي: {remainingStock} / حد التنبيه: {reorderLevel})";
        return SendToAllAsync(title, body, "/Products?status=alert", $"lowstock-{productName}", ct);
    }

    public Task NotifyAbandonedCartAsync(int checkoutId, string customerName, string customerPhone, decimal totalAmount, int itemCount, CancellationToken ct = default)
    {
        var displayName = string.IsNullOrWhiteSpace(customerName) ? customerPhone : customerName;
        var title = "سلة متروكة جديدة";
        var body = $"{displayName} ({customerPhone}) — {itemCount} منتج بقيمة {totalAmount:N0} ج.م";
        return SendToAllAsync(title, body, "/AbandonedCheckouts", $"abandoned-{checkoutId}", ct);
    }

    private async Task<PushVapidSetting> EnsureVapidSettingsAsync(CancellationToken ct)
    {
        var existing = await _context.PushVapidSettings.FirstOrDefaultAsync(ct);
        if (existing != null && !string.IsNullOrWhiteSpace(existing.PublicKey) && !string.IsNullOrWhiteSpace(existing.PrivateKey))
        {
            return existing;
        }

        await VapidLock.WaitAsync(ct);
        try
        {
            existing = await _context.PushVapidSettings.FirstOrDefaultAsync(ct);
            if (existing != null && !string.IsNullOrWhiteSpace(existing.PublicKey) && !string.IsNullOrWhiteSpace(existing.PrivateKey))
            {
                return existing;
            }

            var keys = VapidHelper.GenerateVapidKeys();
            var created = new PushVapidSetting
            {
                Subject = "https://roven-eg.online",
                PublicKey = keys.PublicKey,
                PrivateKey = keys.PrivateKey,
                CreatedAt = DateTime.UtcNow
            };
            _context.PushVapidSettings.Add(created);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Generated and persisted new VAPID keys for Web Push notifications.");
            return created;
        }
        finally
        {
            VapidLock.Release();
        }
    }
}
