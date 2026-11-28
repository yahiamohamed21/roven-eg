namespace Stockify.Services.Interfaces;

public interface IPushNotificationService
{
    Task<string> GetPublicVapidKeyAsync(CancellationToken ct = default);
    Task SubscribeAsync(string endpoint, string p256dh, string auth, string? userId, string? userAgent, CancellationToken ct = default);
    Task UnsubscribeAsync(string endpoint, CancellationToken ct = default);
    Task<int> GetActiveSubscriptionCountAsync(CancellationToken ct = default);
    Task<int> SendToAllAsync(string title, string body, string url = "/Dashboard", string? tag = null, CancellationToken ct = default);
    Task NotifyNewOrderAsync(int orderId, string orderNumber, string customerName, string customerPhone, decimal totalAmount, decimal shippingFee, string? city = null, CancellationToken ct = default);
    Task NotifyLowStockAsync(string productName, int remainingStock, int reorderLevel, CancellationToken ct = default);
    Task NotifyAbandonedCartAsync(int checkoutId, string customerName, string customerPhone, decimal totalAmount, int itemCount, CancellationToken ct = default);
}
