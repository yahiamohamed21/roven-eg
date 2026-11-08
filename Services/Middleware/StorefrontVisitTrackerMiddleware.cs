using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stockify.Common.Constants;
using Stockify.Data;
using Stockify.Models.Entities;

namespace Stockify.Services.Middleware;

public class StorefrontVisitTrackerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StorefrontVisitTrackerMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly string[] ExcludedPrefixes = new[]
    {
        "/dashboard",
        "/orders",
        "/products",
        "/customers",
        "/categories",
        "/account",
        "/abandonedcheckouts",
        "/coupons",
        "/homecontrol",
        "/messages",
        "/reports",
        "/users",
        "/inventory",
        "/lib",
        "/css",
        "/js",
        "/images",
        "/uploads",
        "/api",
        "/home/subscribelead",
        "/checkout/captureabandonedcheckout"
    };

    private static readonly string[] StaticExtensions = new[]
    {
        ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".map", ".webp"
    };

    public StorefrontVisitTrackerMiddleware(
        RequestDelegate next, 
        ILogger<StorefrontVisitTrackerMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Never track visits from authenticated Admins or Employees
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.User.IsInRole(Roles.Admin) || context.User.IsInRole(Roles.Employee))
            {
                await _next(context);
                return;
            }
        }

        // 2. Track only GET requests for public storefront pages
        if (HttpMethods.IsGet(context.Request.Method))
        {
            // Never track background AJAX requests
            if (context.Request.Headers.XRequestedWith == "XMLHttpRequest" ||
                context.Request.Headers.Accept.ToString().Contains("application/json"))
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
            var ext = Path.GetExtension(path);

            // Strictly exclude dashboard, management routes, ajax helpers, and static assets
            if ((string.IsNullOrEmpty(ext) || !StaticExtensions.Contains(ext)) &&
                !ExcludedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
                !path.Contains("getcartsummary") &&
                !path.Contains("captureabandonedcheckout"))
            {
                var throttleKey = $"_visit_ts_{path}";
                var lastVisitStr = context.Session.GetString(throttleKey);
                var now = DateTime.UtcNow;

                bool shouldTrack = true;
                if (!string.IsNullOrEmpty(lastVisitStr) && long.TryParse(lastVisitStr, out var lastTicks))
                {
                    var lastTime = new DateTime(lastTicks, DateTimeKind.Utc);
                    if ((now - lastTime).TotalSeconds < 3)
                    {
                        shouldTrack = false;
                    }
                }

                if (shouldTrack)
                {
                    context.Session.SetString(throttleKey, now.Ticks.ToString());

                    var userAgent = context.Request.Headers.UserAgent.ToString();
                    var referer = context.Request.Headers.Referer.ToString();
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    var sessionId = context.Session.Id;

                    var isMobile = !string.IsNullOrEmpty(userAgent) && 
                                   (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                                    userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                                    userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase));

                    var deviceType = isMobile ? "Mobile" : "Desktop";

                    // Asynchronously persist visit using root scope factory for bulletproof lifecycle safety
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            var visit = new SiteVisit
                            {
                                SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId,
                                IpAddress = ip,
                                UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : (userAgent.Length > 500 ? userAgent[..500] : userAgent),
                                Path = path.Length > 250 ? path[..250] : path,
                                Referer = string.IsNullOrWhiteSpace(referer) ? null : (referer.Length > 500 ? referer[..500] : referer),
                                DeviceType = deviceType,
                                VisitedAt = now
                            };

                            dbContext.SiteVisits.Add(visit);
                            await dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to asynchronously record storefront visit for path {Path}", path);
                        }
                    });
                }
            }
        }

        await _next(context);
    }
}
