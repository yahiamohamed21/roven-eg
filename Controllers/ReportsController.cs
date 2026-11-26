using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Reports;

namespace Stockify.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private sealed record OrderFinancialSummary(
        Order Order,
        decimal ProductSalesNet,
        decimal TotalCost,
        decimal NetProfit);

    private static decimal ResolveEffectiveUnitCost(OrderItem oi)
    {
        if (oi.UnitCost > 0 || oi.UnitProfit > 0)
        {
            return oi.UnitCost;
        }

        if (oi.Product != null)
        {
            if (oi.ProductSizeId.HasValue)
            {
                var size = oi.Product.ProductSizes.FirstOrDefault(s => s.Id == oi.ProductSizeId.Value);
                if (size != null) return size.CostPrice;
            }
            return oi.Product.CostPrice;
        }

        return 0m;
    }

    private static string ResolveEffectiveSizeLabel(OrderItem oi)
    {
        if (!string.IsNullOrWhiteSpace(oi.SizeLabel)) return oi.SizeLabel.Trim();
        if (oi.Product != null && oi.ProductSizeId.HasValue)
        {
            var size = oi.Product.ProductSizes.FirstOrDefault(s => s.Id == oi.ProductSizeId.Value);
            if (size != null && !string.IsNullOrWhiteSpace(size.SizeLabel)) return size.SizeLabel.Trim();
        }
        var def = oi.Product?.ProductSizes.FirstOrDefault(s => s.IsDefault)?.SizeLabel;
        return !string.IsNullOrWhiteSpace(def) ? def.Trim() : "Standard";
    }

    private static OrderFinancialSummary SummarizeOrder(Order o)
    {
        var itemsGross = o.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
        var productSalesNet = itemsGross > 0
            ? Math.Max(0m, itemsGross - o.DiscountAmount)
            : Math.Max(0m, o.TotalAmount - o.ShippingFee);
        var totalCost = o.OrderItems.Sum(i => ResolveEffectiveUnitCost(i) * i.Quantity);
        var netProfit = Math.Round(productSalesNet - totalCost, 2, MidpointRounding.AwayFromZero);
        return new OrderFinancialSummary(o, productSalesNet, Math.Round(totalCost, 2, MidpointRounding.AwayFromZero), netProfit);
    }

    private static List<ProductProfitBreakdownItem> BuildProductProfitBreakdown(IEnumerable<Order> nonCancelledOrders)
    {
        var rows = new Dictionary<(int ProductId, string SizeLabel), ProductProfitBreakdownItem>();

        foreach (var o in nonCancelledOrders)
        {
            var itemsGross = o.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
            decimal discountRatio = (itemsGross > 0 && o.DiscountAmount > 0)
                ? Math.Min(1m, o.DiscountAmount / itemsGross)
                : 0m;

            foreach (var oi in o.OrderItems)
            {
                var sizeLabel = ResolveEffectiveSizeLabel(oi);
                var key = (oi.ProductId, sizeLabel);
                var grossLine = oi.UnitPrice * oi.Quantity;
                var netLineSales = Math.Round(grossLine * (1m - discountRatio), 2, MidpointRounding.AwayFromZero);
                var costLine = Math.Round(ResolveEffectiveUnitCost(oi) * oi.Quantity, 2, MidpointRounding.AwayFromZero);
                var profitLine = Math.Round(netLineSales - costLine, 2, MidpointRounding.AwayFromZero);

                if (!rows.TryGetValue(key, out var item))
                {
                    item = new ProductProfitBreakdownItem
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product?.Name ?? $"Product #{oi.ProductId}",
                        Sku = oi.Product?.Sku ?? "-",
                        SizeLabel = sizeLabel
                    };
                    rows[key] = item;
                }

                item.UnitsSold += oi.Quantity;
                item.TotalRevenue += netLineSales;
                item.TotalCost += costLine;
                item.TotalProfit += profitLine;
            }
        }

        return rows.Values
            .OrderByDescending(x => x.TotalProfit)
            .ThenByDescending(x => x.UnitsSold)
            .ToList();
    }

    // GET: Reports/Index
    public async Task<IActionResult> Index(string mode = "year", int? year = null, int? month = null)
    {
        var nowEgypt = DateTimeExtensions.EgyptNow;
        var startOfMonthUtc = new DateTime(nowEgypt.Year, nowEgypt.Month, 1).ToUtcFromEgypt();
        var endOfMonthUtc = startOfMonthUtc.AddMonths(1);

        var vm = new ReportsViewModel();

        // 1. Live Sales, Revenue, Net Cost & Net Profit Data
        vm.TotalOrdersCount = await _context.Orders.CountAsync();
        vm.MonthlyOrdersCount = await _context.Orders.CountAsync(o => o.OrderDate >= startOfMonthUtc && o.OrderDate < endOfMonthUtc);

        var activeOrders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.ProductSizes)
            .Where(o => o.Status != OrderStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();

        var summaries = activeOrders.Select(SummarizeOrder).ToList();
        var monthlySummaries = summaries
            .Where(s => s.Order.OrderDate >= startOfMonthUtc && s.Order.OrderDate < endOfMonthUtc)
            .ToList();

        vm.TotalRevenueAllTime = summaries.Sum(s => s.Order.TotalAmount);
        vm.TotalNetCostAllTime = summaries.Sum(s => s.TotalCost);
        vm.TotalNetProfitAllTime = summaries.Sum(s => s.NetProfit);
        var allTimeNetSales = summaries.Sum(s => s.ProductSalesNet);
        vm.AllTimeProfitMarginPercent = allTimeNetSales > 0
            ? Math.Round((vm.TotalNetProfitAllTime / allTimeNetSales) * 100m, 1)
            : 0m;

        vm.MonthlyGrossRevenue = monthlySummaries.Sum(s => s.Order.TotalAmount);
        vm.MonthlyNetCost = monthlySummaries.Sum(s => s.TotalCost);
        vm.MonthlyNetProfit = monthlySummaries.Sum(s => s.NetProfit);
        var monthlyNetSales = monthlySummaries.Sum(s => s.ProductSalesNet);
        vm.MonthlyProfitMarginPercent = monthlyNetSales > 0
            ? Math.Round((vm.MonthlyNetProfit / monthlyNetSales) * 100m, 1)
            : 0m;

        vm.AverageOrderValue = summaries.Count > 0
            ? Math.Round(vm.TotalRevenueAllTime / summaries.Count, 2)
            : 0m;

        vm.WebsiteRevenue = summaries.Where(s => s.Order.OrderSource == OrderSource.Website).Sum(s => s.Order.TotalAmount);
        vm.PosRevenue = summaries.Where(s => s.Order.OrderSource == OrderSource.Manual).Sum(s => s.Order.TotalAmount);

        vm.ProductProfitBreakdown = BuildProductProfitBreakdown(activeOrders);

        // 2. Live Inventory Valuation Data (Selling vs Wholesale Cost vs Expected Profit)
        var activeProducts = await _context.Products
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync();

        vm.ActiveProductsCount = activeProducts.Count;
        vm.TotalInventoryUnits = activeProducts.Sum(p => p.StockQuantity);
        vm.TotalStockValuation = activeProducts.Sum(p => p.StockQuantity * p.Price);
        vm.TotalInventoryCostValuation = activeProducts.Sum(p => p.StockQuantity * p.CostPrice);
        vm.TotalInventoryProjectedProfit = activeProducts.Sum(p =>
            p.StockQuantity * ((p.CostPrice > 0 || p.ProfitAmount > 0) ? p.ProfitAmount : p.Price));
        vm.LowStockCount = activeProducts.Count(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel);
        vm.OutOfStockCount = activeProducts.Count(p => p.StockQuantity <= 0);

        // 3. Live Customer Insights Data
        vm.TotalCustomersCount = await _context.Customers.CountAsync();

        var customersWithOrders = await _context.Customers
            .Include(c => c.Orders)
            .Where(c => c.Orders.Any(o => o.Status != OrderStatus.Cancelled))
            .AsNoTracking()
            .ToListAsync();

        vm.ActiveCustomersWithOrders = customersWithOrders.Count;
        vm.RepeatCustomersCount = customersWithOrders.Count(c => c.Orders.Count(o => o.Status != OrderStatus.Cancelled) > 1);

        vm.RepeatPurchaseRate = vm.ActiveCustomersWithOrders > 0
            ? Math.Round(((double)vm.RepeatCustomersCount / vm.ActiveCustomersWithOrders) * 100.0, 1)
            : 0.0;

        vm.AverageCustomerSpend = vm.ActiveCustomersWithOrders > 0
            ? Math.Round(vm.TotalRevenueAllTime / vm.ActiveCustomersWithOrders, 2)
            : 0m;

        // 4. Live Dynamic Sales & Net Profit Trend from Database
        await PopulateChartDataAsync(vm, mode, year, month, summaries);

        return View(vm);
    }

    // GET: Reports/GetSalesChartData (AJAX Endpoint for Dynamic Date Filtering)
    [HttpGet]
    public async Task<IActionResult> GetSalesChartData(string mode = "year", int? year = null, int? month = null)
    {
        var vm = new ReportsViewModel();
        await PopulateChartDataAsync(vm, mode, year, month);

        string periodTitle = vm.FilterMode switch
        {
            "month" => new DateTime(vm.SelectedYear, vm.SelectedMonth, 1).ToString("MMMM yyyy"),
            "last6" => "آخر 6 شهور (Last 6 Months)",
            "last12" => "آخر 12 شهر (Last 12 Months)",
            _ => $"سنة {vm.SelectedYear} (Year {vm.SelectedYear})"
        };

        return Json(new
        {
            success = true,
            labels = vm.ChartMonthLabels,
            actualData = vm.ChartActualRevenue,
            profitData = vm.ChartNetProfit,
            targetData = vm.ChartTargetRevenue,
            ordersCount = vm.ChartOrdersCount,
            totalRevenue = vm.ChartTotalRevenue,
            totalCost = vm.ChartTotalCost,
            totalProfit = vm.ChartTotalProfit,
            profitMargin = vm.ChartProfitMarginPercent,
            totalOrders = vm.ChartTotalOrders,
            averageRevenue = vm.ChartAverageRevenue,
            peakRevenue = vm.ChartPeakRevenue,
            peakLabel = vm.ChartPeakPeriod,
            periodTitle
        });
    }

    private async Task PopulateChartDataAsync(
        ReportsViewModel vm,
        string mode,
        int? year,
        int? month,
        List<OrderFinancialSummary>? preloadedSummaries = null)
    {
        var nowEgypt = DateTimeExtensions.EgyptNow;
        int targetYear = year ?? nowEgypt.Year;
        int targetMonth = month ?? nowEgypt.Month;

        vm.FilterMode = string.IsNullOrWhiteSpace(mode) ? "year" : mode.ToLowerInvariant();
        vm.SelectedYear = targetYear;
        vm.SelectedMonth = targetMonth;

        List<OrderFinancialSummary> allSummaries;
        if (preloadedSummaries != null)
        {
            allSummaries = preloadedSummaries;
        }
        else
        {
            var nonCancelled = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p!.ProductSizes)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .AsNoTracking()
                .ToListAsync();
            allSummaries = nonCancelled.Select(SummarizeOrder).ToList();
        }

        // Populate available years from database orders + current year
        var minOrderYear = allSummaries.Any()
            ? (int?)allSummaries.Min(s => s.Order.OrderDate.Year)
            : null;

        int currentYear = nowEgypt.Year;
        int startYear = minOrderYear.HasValue && minOrderYear.Value > 2000 ? Math.Min(minOrderYear.Value, currentYear - 3) : currentYear - 3;
        vm.AvailableYears = Enumerable.Range(startYear, (currentYear - startYear) + 2).OrderByDescending(y => y).ToList();

        decimal totalPeriodCost = 0m;
        decimal totalPeriodNetSales = 0m;

        if (vm.FilterMode == "month")
        {
            if (targetMonth < 1 || targetMonth > 12) targetMonth = nowEgypt.Month;
            int daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            var mStartEgypt = new DateTime(targetYear, targetMonth, 1);
            var mEndEgypt = mStartEgypt.AddMonths(1);
            var mStartUtc = mStartEgypt.ToUtcFromEgypt();
            var mEndUtc = mEndEgypt.ToUtcFromEgypt();

            var periodOrders = allSummaries
                .Where(s => s.Order.OrderDate >= mStartUtc && s.Order.OrderDate < mEndUtc)
                .ToList();

            var ordersByDay = periodOrders
                .GroupBy(s => s.Order.OrderDate.ToEgyptTime().Day)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Revenue = g.Sum(x => x.Order.TotalAmount),
                        Profit = g.Sum(x => x.NetProfit),
                        Cost = g.Sum(x => x.TotalCost),
                        SalesNet = g.Sum(x => x.ProductSalesNet),
                        Count = g.Count()
                    });

            decimal avgDailyActual = periodOrders.Any() ? periodOrders.Sum(x => x.Order.TotalAmount) / daysInMonth : 0m;

            for (int day = 1; day <= daysInMonth; day++)
            {
                var dayDate = new DateTime(targetYear, targetMonth, day);
                vm.ChartMonthLabels.Add(dayDate.ToString("d MMM"));

                decimal rev = 0m;
                decimal prof = 0m;
                int count = 0;
                if (ordersByDay.TryGetValue(day, out var dayData))
                {
                    rev = dayData.Revenue;
                    prof = dayData.Profit;
                    count = dayData.Count;
                    totalPeriodCost += dayData.Cost;
                    totalPeriodNetSales += dayData.SalesNet;
                }

                vm.ChartActualRevenue.Add(rev);
                vm.ChartNetProfit.Add(prof);
                vm.ChartOrdersCount.Add(count);

                decimal target = rev > 0 ? Math.Round(rev * 1.20m, 0) : (avgDailyActual > 0 ? Math.Round(avgDailyActual * 1.15m, 0) : 0m);
                vm.ChartTargetRevenue.Add(target);
            }
        }
        else if (vm.FilterMode == "last6" || vm.FilterMode == "last12")
        {
            int countMonths = vm.FilterMode == "last12" ? 12 : 6;
            for (int i = countMonths - 1; i >= 0; i--)
            {
                var mDate = nowEgypt.AddMonths(-i);
                var mStartEgypt = new DateTime(mDate.Year, mDate.Month, 1);
                var mEndEgypt = mStartEgypt.AddMonths(1);
                var mStartUtc = mStartEgypt.ToUtcFromEgypt();
                var mEndUtc = mEndEgypt.ToUtcFromEgypt();

                var mOrders = allSummaries
                    .Where(s => s.Order.OrderDate >= mStartUtc && s.Order.OrderDate < mEndUtc)
                    .ToList();

                decimal rev = mOrders.Sum(s => s.Order.TotalAmount);
                decimal prof = mOrders.Sum(s => s.NetProfit);
                totalPeriodCost += mOrders.Sum(s => s.TotalCost);
                totalPeriodNetSales += mOrders.Sum(s => s.ProductSalesNet);
                int count = mOrders.Count;
                decimal target = rev > 0 ? Math.Round(rev * 1.20m, 0) : 0m;

                vm.ChartMonthLabels.Add(mStartEgypt.ToString("MMM yyyy"));
                vm.ChartActualRevenue.Add(rev);
                vm.ChartNetProfit.Add(prof);
                vm.ChartTargetRevenue.Add(target);
                vm.ChartOrdersCount.Add(count);
            }
        }
        else // default: "year" (12 months of selected year)
        {
            vm.FilterMode = "year";
            for (int m = 1; m <= 12; m++)
            {
                var mStartEgypt = new DateTime(targetYear, m, 1);
                var mEndEgypt = mStartEgypt.AddMonths(1);
                var mStartUtc = mStartEgypt.ToUtcFromEgypt();
                var mEndUtc = mEndEgypt.ToUtcFromEgypt();

                var mOrders = allSummaries
                    .Where(s => s.Order.OrderDate >= mStartUtc && s.Order.OrderDate < mEndUtc)
                    .ToList();

                decimal rev = mOrders.Sum(s => s.Order.TotalAmount);
                decimal prof = mOrders.Sum(s => s.NetProfit);
                totalPeriodCost += mOrders.Sum(s => s.TotalCost);
                totalPeriodNetSales += mOrders.Sum(s => s.ProductSalesNet);
                int count = mOrders.Count;
                decimal target = rev > 0 ? Math.Round(rev * 1.20m, 0) : 0m;

                vm.ChartMonthLabels.Add(mStartEgypt.ToString("MMM yyyy"));
                vm.ChartActualRevenue.Add(rev);
                vm.ChartNetProfit.Add(prof);
                vm.ChartTargetRevenue.Add(target);
                vm.ChartOrdersCount.Add(count);
            }
        }

        vm.ChartTotalRevenue = vm.ChartActualRevenue.Sum();
        vm.ChartTotalCost = Math.Round(totalPeriodCost, 2);
        vm.ChartTotalProfit = vm.ChartNetProfit.Sum();
        vm.ChartProfitMarginPercent = totalPeriodNetSales > 0
            ? Math.Round((vm.ChartTotalProfit / totalPeriodNetSales) * 100m, 1)
            : 0m;
        vm.ChartTotalOrders = vm.ChartOrdersCount.Sum();
        vm.ChartAverageRevenue = vm.ChartActualRevenue.Count > 0 ? Math.Round(vm.ChartTotalRevenue / vm.ChartActualRevenue.Count, 2) : 0m;

        decimal peakRevenue = 0m;
        string peakLabel = "-";
        for (int i = 0; i < vm.ChartActualRevenue.Count; i++)
        {
            if (vm.ChartActualRevenue[i] > peakRevenue)
            {
                peakRevenue = vm.ChartActualRevenue[i];
                peakLabel = vm.ChartMonthLabels[i];
            }
        }

        vm.ChartPeakRevenue = peakRevenue;
        vm.ChartPeakPeriod = peakLabel;
    }

    // GET: Reports/ExportStatement (Comprehensive 4-Tab Luxury Excel Workbook)
    [HttpGet]
    public async Task<IActionResult> ExportStatement()
    {
        var nowEgypt = DateTimeExtensions.EgyptNow;
        var startOfMonthUtc = new DateTime(nowEgypt.Year, nowEgypt.Month, 1).ToUtcFromEgypt();
        var endOfMonthUtc = startOfMonthUtc.AddMonths(1);

        using var workbook = new XLWorkbook();

        // -------------------------------------------------------------
        // Sheet 1: Executive Overview & KPIs
        // -------------------------------------------------------------
        var wsOverview = workbook.Worksheets.Add("Executive Overview");
        wsOverview.Cell(1, 1).Value = "ROVEN HAUTE PARFUMERIE - EXECUTIVE BUSINESS INTELLIGENCE";
        wsOverview.Range(1, 1, 1, 4).Merge();
        wsOverview.Cell(1, 1).Style.Font.Bold = true;
        wsOverview.Cell(1, 1).Style.Font.FontSize = 14;
        wsOverview.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsOverview.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        wsOverview.Row(1).Height = 28;

        wsOverview.Cell(2, 1).Value = $"Statement Generated: {nowEgypt:yyyy-MM-dd HH:mm:ss} (Cairo Time) | Live Database Verification";
        wsOverview.Range(2, 1, 2, 4).Merge();
        wsOverview.Cell(2, 1).Style.Font.Italic = true;
        wsOverview.Cell(2, 1).Style.Font.FontSize = 9;
        wsOverview.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6c6358");
        wsOverview.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        wsOverview.Row(2).Height = 18;

        // KPI Table Headers
        wsOverview.Cell(4, 1).Value = "Metric Domain";
        wsOverview.Cell(4, 2).Value = "Key Performance Indicator (KPI)";
        wsOverview.Cell(4, 3).Value = "Current Live Value";
        wsOverview.Cell(4, 4).Value = "Unit / Currency";
        var ovHeader = wsOverview.Range(4, 1, 4, 4);
        ovHeader.Style.Font.Bold = true;
        ovHeader.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
        ovHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
        wsOverview.Row(4).Height = 24;

        var activeExportOrders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.ProductSizes)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();

        var exportSummaries = activeExportOrders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Select(SummarizeOrder)
            .ToList();

        var totalRev = exportSummaries.Sum(s => s.Order.TotalAmount);
        var totalNetCostAll = exportSummaries.Sum(s => s.TotalCost);
        var totalNetProfitAll = exportSummaries.Sum(s => s.NetProfit);
        var monthSummaries = exportSummaries.Where(s => s.Order.OrderDate >= startOfMonthUtc && s.Order.OrderDate < endOfMonthUtc).ToList();
        var monthRev = monthSummaries.Sum(s => s.Order.TotalAmount);
        var monthNetCost = monthSummaries.Sum(s => s.TotalCost);
        var monthNetProfit = monthSummaries.Sum(s => s.NetProfit);
        var websiteRev = exportSummaries.Where(s => s.Order.OrderSource == OrderSource.Website).Sum(s => s.Order.TotalAmount);
        var posRev = exportSummaries.Where(s => s.Order.OrderSource == OrderSource.Manual).Sum(s => s.Order.TotalAmount);
        var totalOrders = activeExportOrders.Count;
        var nonCancelledOrders = exportSummaries.Count;
        var aov = nonCancelledOrders > 0 ? Math.Round(totalRev / nonCancelledOrders, 2) : 0m;

        var products = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).AsNoTracking().ToListAsync();
        var totalStockVal = products.Sum(p => p.StockQuantity * p.Price);
        var totalStockCostVal = products.Sum(p => p.StockQuantity * p.CostPrice);
        var totalStockProjectedProfit = products.Sum(p => p.StockQuantity * ((p.CostPrice > 0 || p.ProfitAmount > 0) ? p.ProfitAmount : p.Price));
        var totalStockUnits = products.Sum(p => p.StockQuantity);
        var lowStockUnits = products.Count(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel);
        var outOfStockCount = products.Count(p => p.StockQuantity <= 0);

        var totalCust = await _context.Customers.CountAsync();
        var custsWithOrders = await _context.Customers.Include(c => c.Orders).Where(c => c.Orders.Any(o => o.Status != OrderStatus.Cancelled)).ToListAsync();
        var repeatCust = custsWithOrders.Count(c => c.Orders.Count(o => o.Status != OrderStatus.Cancelled) > 1);
        var repeatRate = custsWithOrders.Count > 0 ? Math.Round(((double)repeatCust / custsWithOrders.Count) * 100.0, 1) : 0.0;

        var kpiRows = new (string Domain, string Name, object Value, string Unit)[]
        {
            ("Sales & Profit", "Gross Revenue (All Time)", totalRev.ToString("N2"), "EGP"),
            ("Sales & Profit", "Total Net Cost / COGS (All Time)", totalNetCostAll.ToString("N2"), "EGP"),
            ("Sales & Profit", "All-Time Net Profit (صافي المكسب)", totalNetProfitAll.ToString("N2"), "EGP"),
            ("Sales & Profit", "Monthly Gross Revenue (Current Month)", monthRev.ToString("N2"), "EGP"),
            ("Sales & Profit", "Monthly Net Cost (Current Month)", monthNetCost.ToString("N2"), "EGP"),
            ("Sales & Profit", "Monthly Net Profit (صافي مكسب الشهر)", monthNetProfit.ToString("N2"), "EGP"),
            ("Sales & Profit", "E-Commerce Online Revenue", websiteRev.ToString("N2"), "EGP"),
            ("Sales & Profit", "POS Boutique Counter Revenue", posRev.ToString("N2"), "EGP"),
            ("Sales & Profit", "Total Orders Recorded", totalOrders, "Orders"),
            ("Sales & Profit", "Average Order Value (AOV)", aov.ToString("N2"), "EGP / Order"),
            ("Inventory & Flacons", "Total Stock Selling Valuation", totalStockVal.ToString("N2"), "EGP"),
            ("Inventory & Flacons", "Total Stock Wholesale Net Cost", totalStockCostVal.ToString("N2"), "EGP"),
            ("Inventory & Flacons", "Projected Profit From Stock", totalStockProjectedProfit.ToString("N2"), "EGP"),
            ("Inventory & Flacons", "Total Flacons In Reserves", totalStockUnits, "Bottles / Flacons"),
            ("Inventory & Flacons", "Active Fragrance References", products.Count, "SKUs"),
            ("Inventory & Flacons", "Low Stock Fragrance Alerts", lowStockUnits, "Products"),
            ("Inventory & Flacons", "Depleted Reserves (Out of Stock)", outOfStockCount, "Products"),
            ("Patrons & Relationships", "Registered Fragrance Patrons", totalCust, "Patrons"),
            ("Patrons & Relationships", "Active Purchasing Patrons", custsWithOrders.Count, "Patrons"),
            ("Patrons & Relationships", "Repeat Clientele (Loyal Patrons)", repeatCust, "Patrons"),
            ("Patrons & Relationships", "Repeat Purchase Rate", $"{repeatRate}%", "Percentage")
        };

        for (int r = 0; r < kpiRows.Length; r++)
        {
            var rowIdx = 5 + r;
            wsOverview.Cell(rowIdx, 1).Value = kpiRows[r].Domain;
            wsOverview.Cell(rowIdx, 2).Value = kpiRows[r].Name;
            wsOverview.Cell(rowIdx, 3).Value = kpiRows[r].Value.ToString();
            wsOverview.Cell(rowIdx, 3).Style.Font.Bold = true;
            wsOverview.Cell(rowIdx, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsOverview.Cell(rowIdx, 4).Value = kpiRows[r].Unit;
            wsOverview.Cell(rowIdx, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rRange = wsOverview.Range(rowIdx, 1, rowIdx, 4);
            rRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");
            if (r % 2 == 1) rRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            wsOverview.Row(rowIdx).Height = 22;
        }
        wsOverview.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Sheet 2: Sales & Orders Ledger
        // -------------------------------------------------------------
        var wsSales = workbook.Worksheets.Add("Sales & Orders Ledger");
        wsSales.Cell(1, 1).Value = "ROVEN - OMNICHANNEL SALES & PROFIT LEDGER";
        wsSales.Range(1, 1, 1, 11).Merge();
        wsSales.Cell(1, 1).Style.Font.Bold = true;
        wsSales.Cell(1, 1).Style.Font.FontSize = 13;
        wsSales.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsSales.Row(1).Height = 26;

        string[] salesHeaders = { "Order #", "Order Date", "Sales Channel", "Customer", "Phone", "Shipping City", "Units", "Status", "Order Total (EGP)", "Net Cost (EGP)", "Net Profit (EGP)" };
        for (int i = 0; i < salesHeaders.Length; i++)
        {
            var c = wsSales.Cell(3, i + 1);
            c.Value = salesHeaders[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        wsSales.Row(3).Height = 24;

        var allOrders = activeExportOrders;

        int sRow = 4;
        foreach (var ord in allOrders)
        {
            var fin = SummarizeOrder(ord);
            wsSales.Cell(sRow, 1).Value = ord.OrderNumber;
            wsSales.Cell(sRow, 1).Style.Font.Bold = true;
            wsSales.Cell(sRow, 2).Value = ord.OrderDate.ToEgyptTime().ToString("yyyy-MM-dd HH:mm");
            wsSales.Cell(sRow, 3).Value = ord.OrderSource == OrderSource.Website ? "Website / Online" : "POS / Boutique";
            wsSales.Cell(sRow, 4).Value = !string.IsNullOrWhiteSpace(ord.CustomerName) ? ord.CustomerName : (ord.Customer?.FullName ?? "Direct Guest");
            wsSales.Cell(sRow, 5).Value = !string.IsNullOrWhiteSpace(ord.CustomerPhone) ? ord.CustomerPhone : (ord.Customer?.PhoneNumber ?? "-");
            wsSales.Cell(sRow, 6).Value = ord.City ?? "Counter";
            wsSales.Cell(sRow, 7).Value = ord.OrderItems.Sum(i => i.Quantity);
            wsSales.Cell(sRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsSales.Cell(sRow, 8).Value = ord.Status.ToString();
            wsSales.Cell(sRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsSales.Cell(sRow, 9).Value = ord.TotalAmount;
            wsSales.Cell(sRow, 9).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 9).Style.Font.Bold = true;
            wsSales.Cell(sRow, 10).Value = fin.TotalCost;
            wsSales.Cell(sRow, 10).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 11).Value = fin.NetProfit;
            wsSales.Cell(sRow, 11).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 11).Style.Font.Bold = true;
            wsSales.Cell(sRow, 11).Style.Font.FontColor = XLColor.FromHtml("#198754");

            var rRange = wsSales.Range(sRow, 1, sRow, 11);
            rRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");
            if (sRow % 2 == 1) rRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            wsSales.Row(sRow).Height = 20;
            sRow++;
        }
        if (allOrders.Any())
        {
            wsSales.Cell(sRow, 1).Value = "TOTAL (NON-CANCELLED)";
            wsSales.Range(sRow, 1, sRow, 6).Merge();
            wsSales.Cell(sRow, 1).Style.Font.Bold = true;
            wsSales.Cell(sRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsSales.Cell(sRow, 7).Value = allOrders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.OrderItems.Sum(i => i.Quantity));
            wsSales.Cell(sRow, 7).Style.Font.Bold = true;
            wsSales.Cell(sRow, 9).Value = totalRev;
            wsSales.Cell(sRow, 9).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 9).Style.Font.Bold = true;
            wsSales.Cell(sRow, 10).Value = totalNetCostAll;
            wsSales.Cell(sRow, 10).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 10).Style.Font.Bold = true;
            wsSales.Cell(sRow, 11).Value = totalNetProfitAll;
            wsSales.Cell(sRow, 11).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 11).Style.Font.Bold = true;

            var tot = wsSales.Range(sRow, 1, sRow, 11);
            tot.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            tot.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            tot.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            wsSales.Row(sRow).Height = 24;
        }
        wsSales.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Sheet 2b: Profit Breakdown By Product & Size
        // -------------------------------------------------------------
        var wsProfit = workbook.Worksheets.Add("Profit By Product & Size");
        wsProfit.Cell(1, 1).Value = "ROVEN - NET PROFIT BREAKDOWN BY PRODUCT & SIZE";
        wsProfit.Range(1, 1, 1, 7).Merge();
        wsProfit.Cell(1, 1).Style.Font.Bold = true;
        wsProfit.Cell(1, 1).Style.Font.FontSize = 13;
        wsProfit.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsProfit.Row(1).Height = 26;

        string[] profitHeaders = { "SKU", "Fragrance Name", "Size", "Units Sold", "Net Sales (EGP)", "Net Cost (EGP)", "Net Profit (EGP)" };
        for (int i = 0; i < profitHeaders.Length; i++)
        {
            var c = wsProfit.Cell(3, i + 1);
            c.Value = profitHeaders[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
        }
        wsProfit.Row(3).Height = 24;

        var breakdown = BuildProductProfitBreakdown(activeExportOrders.Where(o => o.Status != OrderStatus.Cancelled));
        int pRow = 4;
        foreach (var item in breakdown)
        {
            wsProfit.Cell(pRow, 1).Value = item.Sku;
            wsProfit.Cell(pRow, 2).Value = item.ProductName;
            wsProfit.Cell(pRow, 2).Style.Font.Bold = true;
            wsProfit.Cell(pRow, 3).Value = item.SizeLabel;
            wsProfit.Cell(pRow, 4).Value = item.UnitsSold;
            wsProfit.Cell(pRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsProfit.Cell(pRow, 5).Value = item.TotalRevenue;
            wsProfit.Cell(pRow, 5).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 6).Value = item.TotalCost;
            wsProfit.Cell(pRow, 6).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 7).Value = item.TotalProfit;
            wsProfit.Cell(pRow, 7).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 7).Style.Font.Bold = true;
            wsProfit.Cell(pRow, 7).Style.Font.FontColor = XLColor.FromHtml("#198754");
            wsProfit.Row(pRow).Height = 20;
            pRow++;
        }
        if (breakdown.Any())
        {
            wsProfit.Cell(pRow, 1).Value = "TOTAL";
            wsProfit.Range(pRow, 1, pRow, 3).Merge();
            wsProfit.Cell(pRow, 1).Style.Font.Bold = true;
            wsProfit.Cell(pRow, 4).Value = breakdown.Sum(x => x.UnitsSold);
            wsProfit.Cell(pRow, 5).Value = breakdown.Sum(x => x.TotalRevenue);
            wsProfit.Cell(pRow, 5).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 6).Value = breakdown.Sum(x => x.TotalCost);
            wsProfit.Cell(pRow, 6).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 7).Value = breakdown.Sum(x => x.TotalProfit);
            wsProfit.Cell(pRow, 7).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsProfit.Cell(pRow, 7).Style.Font.Bold = true;
            wsProfit.Range(pRow, 1, pRow, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            wsProfit.Row(pRow).Height = 24;
        }
        wsProfit.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Sheet 3: Perfume Inventory Valuation
        // -------------------------------------------------------------
        var wsInv = workbook.Worksheets.Add("Inventory Valuation");
        wsInv.Cell(1, 1).Value = "ROVEN - ATELIER PERFUME INVENTORY VALUATION & PROFIT";
        wsInv.Range(1, 1, 1, 10).Merge();
        wsInv.Cell(1, 1).Style.Font.Bold = true;
        wsInv.Cell(1, 1).Style.Font.FontSize = 13;
        wsInv.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsInv.Row(1).Height = 26;

        string[] invHeaders = { "SKU", "Flacon / Fragrance Name", "Fragrance Line / Category", "Selling Price (EGP)", "Net Cost (EGP)", "Profit/Unit (EGP)", "Stock Units", "Stock Status", "Total Selling Value (EGP)", "Projected Profit (EGP)" };
        for (int i = 0; i < invHeaders.Length; i++)
        {
            var c = wsInv.Cell(3, i + 1);
            c.Value = invHeaders[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        wsInv.Row(3).Height = 24;

        int iRow = 4;
        foreach (var p in products)
        {
            var status = p.StockQuantity <= 0 ? "Out of Stock" : (p.StockQuantity <= p.ReorderLevel ? "Low Stock" : "Sufficient Stock");
            var val = p.StockQuantity * p.Price;
            var unitProfit = (p.CostPrice > 0 || p.ProfitAmount > 0) ? p.ProfitAmount : p.Price;
            var projProfit = p.StockQuantity * unitProfit;

            wsInv.Cell(iRow, 1).Value = p.Sku;
            wsInv.Cell(iRow, 2).Value = p.Name;
            wsInv.Cell(iRow, 2).Style.Font.Bold = true;
            wsInv.Cell(iRow, 3).Value = p.Category?.Name ?? "-";
            wsInv.Cell(iRow, 4).Value = p.Price;
            wsInv.Cell(iRow, 4).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 5).Value = p.CostPrice;
            wsInv.Cell(iRow, 5).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 6).Value = unitProfit;
            wsInv.Cell(iRow, 6).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 6).Style.Font.FontColor = XLColor.FromHtml("#198754");
            wsInv.Cell(iRow, 7).Value = p.StockQuantity;
            wsInv.Cell(iRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsInv.Cell(iRow, 8).Value = status;
            wsInv.Cell(iRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsInv.Cell(iRow, 9).Value = val;
            wsInv.Cell(iRow, 9).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 9).Style.Font.Bold = true;
            wsInv.Cell(iRow, 10).Value = projProfit;
            wsInv.Cell(iRow, 10).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 10).Style.Font.Bold = true;
            wsInv.Cell(iRow, 10).Style.Font.FontColor = XLColor.FromHtml("#198754");

            var rRange = wsInv.Range(iRow, 1, iRow, 10);
            rRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");
            if (iRow % 2 == 1) rRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            wsInv.Row(iRow).Height = 20;
            iRow++;
        }
        if (products.Any())
        {
            wsInv.Cell(iRow, 1).Value = "TOTAL VALUATION";
            wsInv.Range(iRow, 1, iRow, 6).Merge();
            wsInv.Cell(iRow, 1).Style.Font.Bold = true;
            wsInv.Cell(iRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsInv.Cell(iRow, 7).Value = products.Sum(p => p.StockQuantity);
            wsInv.Cell(iRow, 7).Style.Font.Bold = true;
            wsInv.Cell(iRow, 9).Value = totalStockVal;
            wsInv.Cell(iRow, 9).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 9).Style.Font.Bold = true;
            wsInv.Cell(iRow, 10).Value = totalStockProjectedProfit;
            wsInv.Cell(iRow, 10).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 10).Style.Font.Bold = true;
            wsInv.Cell(iRow, 10).Style.Font.FontColor = XLColor.FromHtml("#198754");

            var tot = wsInv.Range(iRow, 1, iRow, 10);
            tot.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            tot.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            tot.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            wsInv.Row(iRow).Height = 24;
        }
        wsInv.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Sheet 4: Customer Lifetime Value (Patron Analytics)
        // -------------------------------------------------------------
        var wsCust = workbook.Worksheets.Add("Patron Analytics");
        wsCust.Cell(1, 1).Value = "ROVEN - PATRON LIFETIME VALUE & INSIGHTS";
        wsCust.Range(1, 1, 1, 7).Merge();
        wsCust.Cell(1, 1).Style.Font.Bold = true;
        wsCust.Cell(1, 1).Style.Font.FontSize = 13;
        wsCust.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsCust.Row(1).Height = 26;

        string[] custHeaders = { "Patron Name", "Email", "Phone", "Location", "Joined Date", "Orders Count", "Total Lifetime Spend (EGP)" };
        for (int i = 0; i < custHeaders.Length; i++)
        {
            var c = wsCust.Cell(3, i + 1);
            c.Value = custHeaders[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        wsCust.Row(3).Height = 24;

        var allCustomers = await _context.Customers
            .Include(c => c.Orders)
            .OrderByDescending(c => c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount))
            .AsNoTracking()
            .ToListAsync();

        int cRow = 4;
        foreach (var c in allCustomers)
        {
            var spent = c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
            wsCust.Cell(cRow, 1).Value = c.FullName;
            wsCust.Cell(cRow, 1).Style.Font.Bold = true;
            wsCust.Cell(cRow, 2).Value = c.Email;
            wsCust.Cell(cRow, 3).Value = c.PhoneNumber ?? "-";
            wsCust.Cell(cRow, 4).Value = c.Address ?? "-";
            wsCust.Cell(cRow, 5).Value = c.CreatedAt.ToEgyptTime().ToString("yyyy-MM-dd");
            wsCust.Cell(cRow, 6).Value = c.Orders.Count;
            wsCust.Cell(cRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsCust.Cell(cRow, 7).Value = spent;
            wsCust.Cell(cRow, 7).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsCust.Cell(cRow, 7).Style.Font.Bold = true;

            var rRange = wsCust.Range(cRow, 1, cRow, 7);
            rRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");
            if (cRow % 2 == 1) rRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            wsCust.Row(cRow).Height = 20;
            cRow++;
        }
        if (allCustomers.Any())
        {
            wsCust.Cell(cRow, 1).Value = "TOTAL PATRON REVENUE";
            wsCust.Range(cRow, 1, cRow, 5).Merge();
            wsCust.Cell(cRow, 1).Style.Font.Bold = true;
            wsCust.Cell(cRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsCust.Cell(cRow, 6).Value = allCustomers.Sum(c => c.Orders.Count);
            wsCust.Cell(cRow, 6).Style.Font.Bold = true;
            wsCust.Cell(cRow, 7).Value = allCustomers.Sum(c => c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount));
            wsCust.Cell(cRow, 7).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsCust.Cell(cRow, 7).Style.Font.Bold = true;

            var tot = wsCust.Range(cRow, 1, cRow, 7);
            tot.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            tot.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            tot.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            wsCust.Row(cRow).Height = 24;
        }
        wsCust.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var fileName = $"ROVEN_Comprehensive_Statement_{nowEgypt:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
