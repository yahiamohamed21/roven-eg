using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class AbandonedCartPushBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan AbandonmentDelay = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbandonedCartPushBackgroundService> _logger;

    public AbandonedCartPushBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AbandonedCartPushBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay so startup migrations/seeding complete first
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndNotifyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AbandonedCartPushBackgroundService error: {Message}", ex.Message);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CheckAndNotifyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        var hasSubs = await context.PushSubscriptions.AsNoTracking().AnyAsync(ct);
        if (!hasSubs) return;

        var cutoffUtc = DateTime.UtcNow.Subtract(AbandonmentDelay);
        var pendingCheckouts = await context.AbandonedCheckouts
            .Where(a => !a.IsRecovered && a.AdminPushSentAt == null && a.LastUpdatedAt <= cutoffUtc)
            .OrderBy(a => a.LastUpdatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (pendingCheckouts.Count == 0) return;

        foreach (var checkout in pendingCheckouts)
        {
            await pushService.NotifyAbandonedCartAsync(
                checkout.Id,
                checkout.CustomerName ?? string.Empty,
                checkout.CustomerPhone ?? string.Empty,
                checkout.TotalAmount,
                checkout.ItemCount,
                ct);

            checkout.AdminPushSentAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}
