using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Dashboard;

namespace Stockify.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Dashboard
    public async Task<IActionResult> Index(
        string? viewMode = null,
        string? dateRange = "all",
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? salesRepId = null)
    {
        var model = new DashboardViewModel
        {
            DateRange = string.IsNullOrWhiteSpace(dateRange) ? "all" : dateRange.ToLowerInvariant(),
            StartDate = startDate,
            EndDate = endDate,
            SelectedSalesRepId = salesRepId
        };

        var egyptNow = DateTimeExtensions.EgyptNow;
        var todayEgyptStartUtc = egyptNow.Date.ToUtcFromEgypt();

        // 1. Resolve Date Range Window
        DateTime startUtc;
        DateTime endUtc;

        switch (model.DateRange)
        {
            case "today":
                startUtc = egyptNow.Date.ToUtcFromEgypt();
                endUtc = egyptNow.Date.AddDays(1).ToUtcFromEgypt().AddTicks(-1);
                model.DateRangeDescription = $"اليوم ({egyptNow:dd MMM yyyy})";
                break;

            case "yesterday":
                var egyptYesterday = egyptNow.Date.AddDays(-1);
                startUtc = egyptYesterday.ToUtcFromEgypt();
                endUtc = egyptNow.Date.ToUtcFromEgypt().AddTicks(-1);
                model.DateRangeDescription = $"أمس ({egyptYesterday:dd MMM yyyy})";
                break;

            case "last7days":
                startUtc = egyptNow.Date.AddDays(-6).ToUtcFromEgypt();
                endUtc = DateTime.UtcNow.AddMinutes(5);
                model.DateRangeDescription = "آخر 7 أيام (Last 7 Days)";
                break;

            case "thismonth":
                var firstOfMonth = new DateTime(egyptNow.Year, egyptNow.Month, 1);
                startUtc = firstOfMonth.ToUtcFromEgypt();
                endUtc = DateTime.UtcNow.AddMinutes(5);
                model.DateRangeDescription = $"هذا الشهر ({egyptNow:MMMM yyyy})";
                break;

            case "lastmonth":
                var prevMonthDate = egyptNow.Date.AddMonths(-1);
                var firstOfPrevMonth = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1);
                var lastOfPrevMonth = firstOfPrevMonth.AddMonths(1).AddTicks(-1);
                startUtc = firstOfPrevMonth.ToUtcFromEgypt();
                endUtc = lastOfPrevMonth.ToUtcFromEgypt();
                model.DateRangeDescription = $"الشهر الماضي ({prevMonthDate:MMMM yyyy})";
                break;

            case "thisyear":
                var firstOfYear = new DateTime(egyptNow.Year, 1, 1);
                startUtc = firstOfYear.ToUtcFromEgypt();
                endUtc = DateTime.UtcNow.AddMinutes(5);
                model.DateRangeDescription = $"هذا العام ({egyptNow.Year})";
                break;

            case "custom":
                startUtc = startDate.HasValue 
                    ? startDate.Value.Date.ToUtcFromEgypt() 
                    : new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endUtc = endDate.HasValue 
                    ? endDate.Value.Date.AddDays(1).ToUtcFromEgypt().AddTicks(-1) 
                    : DateTime.UtcNow.AddMinutes(5);
                model.DateRangeDescription = $"فترة مخصصة ({startUtc.ToEgyptTime():dd/MM/yyyy} إلى {endUtc.ToEgyptTime():dd/MM/yyyy})";
                break;

            case "all":
            default:
                model.DateRange = "all";
                startUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endUtc = DateTime.UtcNow.AddMinutes(5);
                model.DateRangeDescription = "كل الأوقات (All Time)";
                break;
        }

        // 2. Query Eligible Sales Representatives (Optimized Single Join)
        var eligibleUsers = await (
            from u in _context.Users.AsNoTracking()
            join ur in _context.UserRoles on u.Id equals ur.UserId
            join r in _context.Roles on ur.RoleId equals r.Id
            where r.Name == Roles.Employee || r.Name == Roles.Admin
            select u
        ).Distinct().OrderBy(u => u.FullName).ToListAsync();

        var salesUserIds = eligibleUsers.Select(u => u.Id).ToList();

        model.AvailableSalesReps = eligibleUsers.Select(u => new SalesRepDropdownItemViewModel
        {
            Id = u.Id,
            FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : (!string.IsNullOrWhiteSpace(u.UserName) ? u.UserName : "Sales Rep"),
            Email = u.Email ?? ""
        }).ToList();

        // 3. Resolve Specific Sales Rep Filtering (Account Inception vs Selected Period)
        IQueryable<int>? selectedRepOrderIdsQuery = null;

        if (!string.IsNullOrEmpty(salesRepId) && salesRepId != "all")
        {
            var selectedRepUser = eligibleUsers.FirstOrDefault(u => u.Id == salesRepId);
            if (selectedRepUser != null)
            {
                selectedRepOrderIdsQuery = _context.InventoryTransactions
                    .Where(t => t.UserId == salesRepId && t.OrderId != null && t.TransactionType == TransactionType.StockOut)
                    .Select(t => t.OrderId!.Value)
                    .Distinct();

                // Lifetime metrics: from the exact date account was created (CreatedAt) up to endUtc
                var repLifetimeOrders = await _context.Orders
                    .Where(o => o.Status != OrderStatus.Cancelled
                             && selectedRepOrderIdsQuery.Contains(o.Id)
                             && o.OrderDate >= selectedRepUser.CreatedAt
                             && o.OrderDate <= endUtc)
                    .ToListAsync();

                var lifetimeRevenue = repLifetimeOrders.Sum(o => o.TotalAmount);
                var lifetimeOrdersCount = repLifetimeOrders.Count;

                // Filtered Period metrics: within startUtc to endUtc
                var repPeriodOrders = repLifetimeOrders
                    .Where(o => o.OrderDate >= startUtc)
                    .ToList();

                var periodRevenue = repPeriodOrders.Sum(o => o.TotalAmount);
                var periodOrdersCount = repPeriodOrders.Count;
                var periodOrderIds = repPeriodOrders.Select(o => o.Id).ToList();

                var periodFlaconsSold = await _context.OrderItems
                    .Where(oi => periodOrderIds.Contains(oi.OrderId))
                    .SumAsync(oi => (int?)oi.Quantity) ?? 0;

                model.SelectedSalesRepDetails = new SelectedSalesRepDetailViewModel
                {
                    Id = selectedRepUser.Id,
                    FullName = !string.IsNullOrWhiteSpace(selectedRepUser.FullName) ? selectedRepUser.FullName : (selectedRepUser.UserName ?? "Sales Rep"),
                    Email = selectedRepUser.Email ?? "",
                    Initials = GetInitials(selectedRepUser.FullName ?? selectedRepUser.UserName ?? "SR"),
                    AccountCreatedAt = selectedRepUser.CreatedAt,
                    LifetimeRevenue = lifetimeRevenue,
                    LifetimeOrdersCount = lifetimeOrdersCount,
                    PeriodRevenue = periodRevenue,
                    PeriodOrdersCount = periodOrdersCount,
                    PeriodFlaconsSold = periodFlaconsSold,
                    TargetQuota = 50000m
                };
            }
        }

        // 4. Base Orders Query (Filtered by Date Range and optional Sales Rep)
        var ordersQuery = _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.OrderDate >= startUtc && o.OrderDate <= endUtc);

        if (selectedRepOrderIdsQuery != null)
        {
            ordersQuery = ordersQuery.Where(o => selectedRepOrderIdsQuery.Contains(o.Id));
        }

        // 5. High-Level Inventory & Sales KPIs (Combined Single-Batch Aggregate Queries)
        var productStats = await _context.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                LowStock = g.Count(p => p.StockQuantity <= p.ReorderLevel),
                OutOfStock = g.Count(p => p.StockQuantity <= 0)
            })
            .FirstOrDefaultAsync();

        model.TotalProducts = productStats?.Total ?? 0;
        model.LowStockItemsCount = productStats?.LowStock ?? 0;
        model.OutOfStockCount = productStats?.OutOfStock ?? 0;
        model.TotalCustomers = await _context.Customers.AsNoTracking().CountAsync(c => c.CreatedAt <= endUtc);

        var orderStats = await ordersQuery.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(o => o.TotalAmount),
                TodayRevenue = g.Where(o => o.OrderDate >= todayEgyptStartUtc).Sum(o => o.TotalAmount),
                TodayOrders = g.Count(o => o.OrderDate >= todayEgyptStartUtc),
                WebsiteOrders = g.Count(o => o.OrderSource == OrderSource.Website),
                WebsiteRevenue = g.Where(o => o.OrderSource == OrderSource.Website).Sum(o => o.TotalAmount),
                ManualOrders = g.Count(o => o.OrderSource == OrderSource.Manual),
                ManualRevenue = g.Where(o => o.OrderSource == OrderSource.Manual).Sum(o => o.TotalAmount)
            })
            .FirstOrDefaultAsync();

        model.TotalOrders = orderStats?.TotalOrders ?? 0;
        model.TotalSalesRevenue = orderStats?.TotalRevenue ?? 0m;
        model.TodaySalesRevenue = orderStats?.TodayRevenue ?? 0m;
        model.TodayOrdersCount = orderStats?.TodayOrders ?? 0;
        model.WebsiteOrdersCount = orderStats?.WebsiteOrders ?? 0;
        model.WebsiteSalesRevenue = orderStats?.WebsiteRevenue ?? 0m;
        model.ManualOrdersCount = orderStats?.ManualOrders ?? 0;
        model.ManualSalesRevenue = orderStats?.ManualRevenue ?? 0m;

        model.PendingWebsiteOrdersCount = await _context.Orders.AsNoTracking()
            .CountAsync(o => o.OrderSource == OrderSource.Website && o.Status == OrderStatus.Pending && o.OrderDate >= startUtc && o.OrderDate <= endUtc);

        // Website Visits & Traffic Analytics (Single Aggregation Query)
        var visitStats = await _context.SiteVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= startUtc && v.VisitedAt <= endUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Today = g.Count(v => v.VisitedAt >= todayEgyptStartUtc),
                Unique = g.Select(v => v.SessionId ?? v.IpAddress).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        model.TotalWebsiteVisits = visitStats?.Total ?? 0;
        model.TodayWebsiteVisits = visitStats?.Today ?? 0;
        model.UniqueVisitorsCount = visitStats?.Unique ?? 0;

        // Abandoned Checkouts Analytics (Single Aggregation Query)
        var abandonedStats = await _context.AbandonedCheckouts.AsNoTracking()
            .Where(a => !a.IsRecovered && a.LastUpdatedAt >= startUtc && a.LastUpdatedAt <= endUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalValue = g.Sum(a => a.TotalAmount)
            })
            .FirstOrDefaultAsync();

        model.AbandonedCheckoutsCount = abandonedStats?.Count ?? 0;
        model.AbandonedCheckoutsTotalValue = abandonedStats?.TotalValue ?? 0m;

        model.RecentAbandonedCheckouts = await _context.AbandonedCheckouts
            .Where(a => a.LastUpdatedAt >= startUtc && a.LastUpdatedAt <= endUtc)
            .OrderByDescending(a => a.LastUpdatedAt)
            .Take(8)
            .Select(a => new AbandonedCheckoutDashboardViewModel
            {
                Id = a.Id,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                CustomerEmail = a.CustomerEmail,
                City = a.City,
                ShippingAddress = a.ShippingAddress,
                CartItemsJson = a.CartItemsJson,
                TotalAmount = a.TotalAmount,
                ItemCount = a.ItemCount,
                CreatedAt = a.CreatedAt,
                LastUpdatedAt = a.LastUpdatedAt,
                IsRecovered = a.IsRecovered,
                RecoveredOrderId = a.RecoveredOrderId
            })
            .ToListAsync();

        // 6. Dynamic Sales Trend Chart Data (Grouped by Day according to Date Filter)
        var chartStartDay = startUtc.ToEgyptTime().Date;
        var chartEndDay = endUtc.ToEgyptTime().Date > egyptNow.Date ? egyptNow.Date : endUtc.ToEgyptTime().Date;

        var totalDaysInRange = (chartEndDay - chartStartDay).Days + 1;
        List<DateTime> chartDays;

        if (totalDaysInRange <= 31 && totalDaysInRange > 0)
        {
            chartDays = Enumerable.Range(0, totalDaysInRange)
                .Select(i => chartStartDay.AddDays(i))
                .ToList();
        }
        else
        {
            // For longer ranges (e.g. All time or full year), display the last 30 daily data points
            chartDays = Enumerable.Range(0, 30)
                .Select(i => chartEndDay.AddDays(-29 + i))
                .ToList();
        }

        var chartMinUtc = chartDays.First().ToUtcFromEgypt();
        var chartOrders = await ordersQuery
            .Where(o => o.OrderDate >= chartMinUtc && o.OrderDate <= endUtc)
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync();

        var dailyOrdersGroup = chartOrders
            .GroupBy(o => o.OrderDate.ToEgyptTime().Date)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(o => o.TotalAmount),
                Count = g.Count()
            })
            .ToList();

        foreach (var day in chartDays)
        {
            model.ChartLabels.Add(day.ToString("ddd, dd MMM"));
            var match = dailyOrdersGroup.FirstOrDefault(x => x.Date == day);
            model.ChartRevenueData.Add(match?.Revenue ?? 0m);
            model.ChartOrderCountData.Add(match?.Count ?? 0);
        }

        // 7. Top Selling Products (Filtered to Window & Sales Rep)
        var orderIdsForProducts = await ordersQuery.Select(o => o.Id).ToListAsync();

        var topSelling = await _context.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => orderIdsForProducts.Contains(oi.OrderId))
            .GroupBy(oi => new { oi.ProductId, oi.Product!.Name, oi.Product.Sku, oi.Product.Price })
            .Select(g => new TopSellingProductViewModel
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                Sku = g.Key.Sku,
                UnitPrice = g.Key.Price,
                UnitsSold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.UnitsSold)
            .Take(5)
            .ToListAsync();

        if (topSelling.Any())
        {
            model.TopSellingProducts = topSelling;
        }
        else
        {
            model.TopSellingProducts = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Take(5)
                .Select(p => new TopSellingProductViewModel
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Sku = p.Sku,
                    UnitPrice = p.Price,
                    UnitsSold = 0,
                    TotalRevenue = 0m
                })
                .ToListAsync();
        }

        // 8. Recent Orders (Within Filtered Window)
        model.RecentOrders = await ordersQuery
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .Take(6)
            .Select(o => new RecentOrderDashboardViewModel
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = !string.IsNullOrWhiteSpace(o.CustomerName) ? o.CustomerName : (o.Customer != null ? o.Customer.FullName : "Walk-in Retail"),
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                OrderSource = o.OrderSource,
                ItemsCount = o.OrderItems.Count
            })
            .ToListAsync();

        // 9. Low Stock Products
        model.LowStockProducts = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .Take(5)
            .Select(p => new LowStockDashboardViewModel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "General",
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel
            })
            .ToListAsync();

        // 10. Sales Representatives Team Leaderboard (Aggregated for Selected Date Range)
        var repStats = await _context.InventoryTransactions
            .Where(t => t.UserId != null
                     && t.OrderId != null
                     && t.TransactionType == TransactionType.StockOut
                     && t.Order != null
                     && t.Order.Status != OrderStatus.Cancelled
                     && t.Order.OrderDate >= startUtc
                     && t.Order.OrderDate <= endUtc
                     && salesUserIds.Contains(t.UserId))
            .Select(t => new { t.UserId, t.OrderId, t.Order!.TotalAmount })
            .Distinct()
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key!,
                OrdersCount = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .ToDictionaryAsync(x => x.UserId);

        foreach (var u in eligibleUsers)
        {
            var hasStats = repStats.TryGetValue(u.Id, out var stats) && stats != null;
            var ordersCount = hasStats ? stats!.OrdersCount : 0;
            var totalRevenue = hasStats ? stats!.TotalRevenue : 0m;

            model.SalesRepsPerformance.Add(new SalesRepPerformanceViewModel
            {
                UserId = u.Id,
                RepName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : (!string.IsNullOrWhiteSpace(u.UserName) ? u.UserName : "Sales Rep"),
                Email = u.Email ?? "",
                Initials = GetInitials(u.FullName ?? u.UserName ?? "SR"),
                OrdersCount = ordersCount,
                TotalRevenue = totalRevenue,
                TargetQuota = 50000m
            });
        }

        model.SalesRepsPerformance = model.SalesRepsPerformance
            .OrderByDescending(r => r.TotalRevenue)
            .ThenByDescending(r => r.OrdersCount)
            .ThenBy(r => r.RepName)
            .ToList();

        // View Routing for Sales Rep vs Admin View
        if (viewMode == "sales" || (User.IsInRole(Roles.Employee) && viewMode != "admin"))
        {
            ViewData["ViewMode"] = "sales";
            return View("EmployeeIndex", model);
        }

        ViewData["ViewMode"] = "admin";
        return View("Index", model);
    }

    public IActionResult SalesRep()
    {
        return RedirectToAction(nameof(Index), new { viewMode = "sales" });
    }

    [HttpGet]
    public async Task<IActionResult> GetLiveNotifications()
    {
        DateTime? notifsLastReadAt = null;
        if (Request.Cookies.TryGetValue("roven_notifs_read_at", out var readAtStr) &&
            long.TryParse(readAtStr, out var readAtMs))
        {
            notifsLastReadAt = DateTimeOffset.FromUnixTimeMilliseconds(readAtMs).UtcDateTime;
        }

        var readOrderIds = new HashSet<int>();
        if (Request.Cookies.TryGetValue("roven_read_orders", out var readOrdersCookie) && !string.IsNullOrEmpty(readOrdersCookie))
        {
            foreach (var part in readOrdersCookie.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var parsedId))
                {
                    readOrderIds.Add(parsedId);
                }
            }
        }

        var pendingOrdersQuery = _context.Orders
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing);

        if (notifsLastReadAt.HasValue)
        {
            pendingOrdersQuery = pendingOrdersQuery.Where(o => o.OrderDate > notifsLastReadAt.Value);
        }

        var pendingOrdersCount = await pendingOrdersQuery.CountAsync(o => !readOrderIds.Contains(o.Id));

        var lowStockCount = await _context.Products
            .CountAsync(p => p.IsActive && p.StockQuantity <= p.ReorderLevel);

        if (notifsLastReadAt.HasValue)
        {
            lowStockCount = 0;
        }

        var unreadMessagesCount = 0;
        if (User.IsInRole(Roles.Admin))
        {
            var msgQuery = _context.ContactMessages
                .Where(m => !m.IsRead);
            if (notifsLastReadAt.HasValue)
            {
                msgQuery = msgQuery.Where(m => m.CreatedAt > notifsLastReadAt.Value);
            }
            unreadMessagesCount = await msgQuery.CountAsync();
        }

        var totalAlerts = pendingOrdersCount + lowStockCount + unreadMessagesCount;

        return Json(new
        {
            success = true,
            totalAlerts,
            pendingOrdersCount,
            lowStockCount,
            unreadMessagesCount
        });
    }

    #region Helper Methods

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "SR";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
    }

    #endregion
}
