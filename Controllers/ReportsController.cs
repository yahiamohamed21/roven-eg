using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Extensions;
using Stockify.Data;
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

    // GET: Reports/Index
    public async Task<IActionResult> Index()
    {
        var nowEgypt = DateTimeExtensions.EgyptNow;
        var startOfMonthUtc = new DateTime(nowEgypt.Year, nowEgypt.Month, 1).ToUtcFromEgypt();
        var endOfMonthUtc = startOfMonthUtc.AddMonths(1);

        var vm = new ReportsViewModel();

        // 1. Live Sales & Revenue Data
        vm.MonthlyGrossRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.OrderDate >= startOfMonthUtc && o.OrderDate < endOfMonthUtc)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        vm.TotalRevenueAllTime = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        vm.MonthlyOrdersCount = await _context.Orders
            .CountAsync(o => o.OrderDate >= startOfMonthUtc && o.OrderDate < endOfMonthUtc);

        vm.TotalOrdersCount = await _context.Orders.CountAsync();

        var completedOrActiveOrdersCount = await _context.Orders
            .CountAsync(o => o.Status != OrderStatus.Cancelled);

        vm.AverageOrderValue = completedOrActiveOrdersCount > 0
            ? Math.Round(vm.TotalRevenueAllTime / completedOrActiveOrdersCount, 2)
            : 0m;

        vm.WebsiteRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.OrderSource == OrderSource.Website)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        vm.PosRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.OrderSource == OrderSource.Manual)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        // 2. Live Inventory Valuation Data
        var activeProducts = await _context.Products
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync();

        vm.ActiveProductsCount = activeProducts.Count;
        vm.TotalInventoryUnits = activeProducts.Sum(p => p.StockQuantity);
        vm.TotalStockValuation = activeProducts.Sum(p => p.StockQuantity * p.Price);
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

        // 4. Live Monthly Sales Trend (Last 6 Months from Database)
        for (int i = 5; i >= 0; i--)
        {
            var mDate = nowEgypt.AddMonths(-i);
            var mStartEgypt = new DateTime(mDate.Year, mDate.Month, 1);
            var mEndEgypt = mStartEgypt.AddMonths(1);
            var mStartUtc = mStartEgypt.ToUtcFromEgypt();
            var mEndUtc = mEndEgypt.ToUtcFromEgypt();
            var label = mStartEgypt.ToString("MMM yyyy");

            var actualRev = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelled && o.OrderDate >= mStartUtc && o.OrderDate < mEndUtc)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            var targetRev = actualRev > 0 ? Math.Round(actualRev * 1.20m, 0) : 25000m;

            vm.ChartMonthLabels.Add(label);
            vm.ChartActualRevenue.Add(actualRev);
            vm.ChartTargetRevenue.Add(targetRev);
        }

        return View(vm);
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

        // Data queries
        var totalRev = await _context.Orders.Where(o => o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var monthRev = await _context.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.OrderDate >= startOfMonthUtc && o.OrderDate < endOfMonthUtc).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var websiteRev = await _context.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.OrderSource == OrderSource.Website).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var posRev = await _context.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.OrderSource == OrderSource.Manual).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var totalOrders = await _context.Orders.CountAsync();
        var nonCancelledOrders = await _context.Orders.CountAsync(o => o.Status != OrderStatus.Cancelled);
        var aov = nonCancelledOrders > 0 ? Math.Round(totalRev / nonCancelledOrders, 2) : 0m;

        var products = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).AsNoTracking().ToListAsync();
        var totalStockVal = products.Sum(p => p.StockQuantity * p.Price);
        var totalStockUnits = products.Sum(p => p.StockQuantity);
        var lowStockUnits = products.Count(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel);
        var outOfStockCount = products.Count(p => p.StockQuantity <= 0);

        var totalCust = await _context.Customers.CountAsync();
        var custsWithOrders = await _context.Customers.Include(c => c.Orders).Where(c => c.Orders.Any(o => o.Status != OrderStatus.Cancelled)).ToListAsync();
        var repeatCust = custsWithOrders.Count(c => c.Orders.Count(o => o.Status != OrderStatus.Cancelled) > 1);
        var repeatRate = custsWithOrders.Count > 0 ? Math.Round(((double)repeatCust / custsWithOrders.Count) * 100.0, 1) : 0.0;

        var kpiRows = new (string Domain, string Name, object Value, string Unit)[]
        {
            ("Sales & Revenue", "Gross Revenue (All Time)", totalRev.ToString("N2"), "EGP"),
            ("Sales & Revenue", "Monthly Gross Revenue (Current Month)", monthRev.ToString("N2"), "EGP"),
            ("Sales & Revenue", "E-Commerce Online Revenue", websiteRev.ToString("N2"), "EGP"),
            ("Sales & Revenue", "POS Boutique Counter Revenue", posRev.ToString("N2"), "EGP"),
            ("Sales & Revenue", "Total Orders Recorded", totalOrders, "Orders"),
            ("Sales & Revenue", "Average Order Value (AOV)", aov.ToString("N2"), "EGP / Order"),
            ("Inventory & Flacons", "Total Stock Asset Valuation", totalStockVal.ToString("N2"), "EGP"),
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
        wsSales.Cell(1, 1).Value = "ROVEN - OMNICHANNEL SALES TRANSACTIONS LEDGER";
        wsSales.Range(1, 1, 1, 9).Merge();
        wsSales.Cell(1, 1).Style.Font.Bold = true;
        wsSales.Cell(1, 1).Style.Font.FontSize = 13;
        wsSales.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsSales.Row(1).Height = 26;

        string[] salesHeaders = { "Order #", "Order Date", "Sales Channel", "Customer", "Phone", "Shipping City", "Units", "Status", "Amount (EGP)" };
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

        var allOrders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();

        int sRow = 4;
        foreach (var ord in allOrders)
        {
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

            var rRange = wsSales.Range(sRow, 1, sRow, 9);
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
            wsSales.Cell(sRow, 9).Value = allOrders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
            wsSales.Cell(sRow, 9).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsSales.Cell(sRow, 9).Style.Font.Bold = true;

            var tot = wsSales.Range(sRow, 1, sRow, 9);
            tot.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            tot.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            tot.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            wsSales.Row(sRow).Height = 24;
        }
        wsSales.Columns().AdjustToContents();

        // -------------------------------------------------------------
        // Sheet 3: Perfume Inventory Valuation
        // -------------------------------------------------------------
        var wsInv = workbook.Worksheets.Add("Inventory Valuation");
        wsInv.Cell(1, 1).Value = "ROVEN - ATELIER PERFUME INVENTORY VALUATION";
        wsInv.Range(1, 1, 1, 8).Merge();
        wsInv.Cell(1, 1).Style.Font.Bold = true;
        wsInv.Cell(1, 1).Style.Font.FontSize = 13;
        wsInv.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        wsInv.Row(1).Height = 26;

        string[] invHeaders = { "SKU", "Flacon / Fragrance Name", "Fragrance Line / Category", "Unit Price (EGP)", "Stock Units", "Threshold", "Stock Status", "Total Asset Value (EGP)" };
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

            wsInv.Cell(iRow, 1).Value = p.Sku;
            wsInv.Cell(iRow, 2).Value = p.Name;
            wsInv.Cell(iRow, 2).Style.Font.Bold = true;
            wsInv.Cell(iRow, 3).Value = p.Category?.Name ?? "-";
            wsInv.Cell(iRow, 4).Value = p.Price;
            wsInv.Cell(iRow, 4).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 5).Value = p.StockQuantity;
            wsInv.Cell(iRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsInv.Cell(iRow, 6).Value = p.ReorderLevel;
            wsInv.Cell(iRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsInv.Cell(iRow, 7).Value = status;
            wsInv.Cell(iRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsInv.Cell(iRow, 8).Value = val;
            wsInv.Cell(iRow, 8).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 8).Style.Font.Bold = true;

            var rRange = wsInv.Range(iRow, 1, iRow, 8);
            rRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");
            if (iRow % 2 == 1) rRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            wsInv.Row(iRow).Height = 20;
            iRow++;
        }
        if (products.Any())
        {
            wsInv.Cell(iRow, 1).Value = "TOTAL VALUATION";
            wsInv.Range(iRow, 1, iRow, 4).Merge();
            wsInv.Cell(iRow, 1).Style.Font.Bold = true;
            wsInv.Cell(iRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsInv.Cell(iRow, 5).Value = products.Sum(p => p.StockQuantity);
            wsInv.Cell(iRow, 5).Style.Font.Bold = true;
            wsInv.Cell(iRow, 8).Value = products.Sum(p => p.StockQuantity * p.Price);
            wsInv.Cell(iRow, 8).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            wsInv.Cell(iRow, 8).Style.Font.Bold = true;

            var tot = wsInv.Range(iRow, 1, iRow, 8);
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
