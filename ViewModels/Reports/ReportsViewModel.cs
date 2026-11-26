namespace Stockify.ViewModels.Reports;

public class ReportsViewModel
{
    // 1. Sales & Revenue KPIs (Calculated from live database)
    public decimal MonthlyGrossRevenue { get; set; }
    public decimal TotalRevenueAllTime { get; set; }
    public decimal TotalNetCostAllTime { get; set; }
    public decimal TotalNetProfitAllTime { get; set; }
    public decimal AllTimeProfitMarginPercent { get; set; }
    public decimal MonthlyNetCost { get; set; }
    public decimal MonthlyNetProfit { get; set; }
    public decimal MonthlyProfitMarginPercent { get; set; }
    public int MonthlyOrdersCount { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal WebsiteRevenue { get; set; }
    public decimal PosRevenue { get; set; }

    // 2. Inventory Valuation KPIs (Calculated from live database)
    public decimal TotalStockValuation { get; set; }
    public decimal TotalInventoryCostValuation { get; set; }
    public decimal TotalInventoryProjectedProfit { get; set; }
    public int TotalInventoryUnits { get; set; }
    public int ActiveProductsCount { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }

    // 3. Customer Lifetime Insights (Calculated from live database)
    public int TotalCustomersCount { get; set; }
    public int ActiveCustomersWithOrders { get; set; }
    public int RepeatCustomersCount { get; set; }
    public double RepeatPurchaseRate { get; set; }
    public decimal AverageCustomerSpend { get; set; }

    // 4. Monthly Chart Data (Calculated from live database)
    public List<string> ChartMonthLabels { get; set; } = new();
    public List<decimal> ChartActualRevenue { get; set; } = new();
    public List<decimal> ChartNetProfit { get; set; } = new();
    public List<decimal> ChartTargetRevenue { get; set; } = new();
    public List<int> ChartOrdersCount { get; set; } = new();

    // 5. Chart Filters & Live Performance Aggregation
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public string FilterMode { get; set; } = "year";
    public List<int> AvailableYears { get; set; } = new();
    public decimal ChartTotalRevenue { get; set; }
    public decimal ChartTotalCost { get; set; }
    public decimal ChartTotalProfit { get; set; }
    public decimal ChartProfitMarginPercent { get; set; }
    public int ChartTotalOrders { get; set; }
    public decimal ChartAverageRevenue { get; set; }
    public string ChartPeakPeriod { get; set; } = "-";
    public decimal ChartPeakRevenue { get; set; }

    // 6. Per-Product & Size Profit Breakdown ("أنا كسبت قد إيه")
    public List<ProductProfitBreakdownItem> ProductProfitBreakdown { get; set; } = new();
}

public class ProductProfitBreakdownItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal MarginPercent => TotalRevenue > 0 ? Math.Round((TotalProfit / TotalRevenue) * 100m, 1) : 0m;
}

