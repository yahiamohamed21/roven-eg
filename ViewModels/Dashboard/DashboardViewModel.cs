using Stockify.Models.Enums;

namespace Stockify.ViewModels.Dashboard;

public class DashboardViewModel
{
    // High-Level KPIs
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public decimal TotalSalesRevenue { get; set; }
    public int LowStockItemsCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal TodaySalesRevenue { get; set; }
    public int TodayOrdersCount { get; set; }

    // E-Commerce Metrics
    public int WebsiteOrdersCount { get; set; }
    public int PendingWebsiteOrdersCount { get; set; }
    public decimal WebsiteSalesRevenue { get; set; }
    public int ManualOrdersCount { get; set; }
    public decimal ManualSalesRevenue { get; set; }

    // Website Visits & Traffic Analytics
    public int TotalWebsiteVisits { get; set; }
    public int TodayWebsiteVisits { get; set; }
    public int UniqueVisitorsCount { get; set; }

    // Abandoned Checkouts Analytics
    public int AbandonedCheckoutsCount { get; set; }
    public decimal AbandonedCheckoutsTotalValue { get; set; }
    public List<AbandonedCheckoutDashboardViewModel> RecentAbandonedCheckouts { get; set; } = new();

    // Chart Data
    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> ChartRevenueData { get; set; } = new();
    public List<int> ChartOrderCountData { get; set; } = new();

    // Filter State & Metadata
    public string DateRange { get; set; } = "all";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SelectedSalesRepId { get; set; }
    public string DateRangeDescription { get; set; } = "All Time (كل الأوقات)";
    public List<SalesRepDropdownItemViewModel> AvailableSalesReps { get; set; } = new();
    public SelectedSalesRepDetailViewModel? SelectedSalesRepDetails { get; set; }

    // Tables Data
    public List<TopSellingProductViewModel> TopSellingProducts { get; set; } = new();
    public List<RecentOrderDashboardViewModel> RecentOrders { get; set; } = new();
    public List<LowStockDashboardViewModel> LowStockProducts { get; set; } = new();
    public List<SalesRepPerformanceViewModel> SalesRepsPerformance { get; set; } = new();
}

public class SalesRepDropdownItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class SelectedSalesRepDetailViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Initials { get; set; } = "SR";
    public DateTime AccountCreatedAt { get; set; }

    // Lifetime metrics (from account creation date to filter end date)
    public decimal LifetimeRevenue { get; set; }
    public int LifetimeOrdersCount { get; set; }
    public decimal LifetimeCommission => LifetimeRevenue * 0.05m;

    // Filtered Period metrics (within chosen date range)
    public decimal PeriodRevenue { get; set; }
    public int PeriodOrdersCount { get; set; }
    public int PeriodFlaconsSold { get; set; }
    public decimal PeriodCommission => PeriodRevenue * 0.05m;
    public decimal TargetQuota { get; set; } = 50000m;
    public decimal TargetAttainment => TargetQuota > 0 ? Math.Min(100m, (PeriodRevenue / TargetQuota) * 100m) : 0m;
    public decimal AverageOrderValue => PeriodOrdersCount > 0 ? PeriodRevenue / PeriodOrdersCount : 0m;
}

public class TopSellingProductViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int UnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RecentOrderDashboardViewModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public OrderSource OrderSource { get; set; } = OrderSource.Manual;
    public int ItemsCount { get; set; }
}

public class LowStockDashboardViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
}

public class SalesRepPerformanceViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string RepName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Initials { get; set; } = "SR";
    public int OrdersCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TargetQuota { get; set; } = 50000m;
    public decimal AttainmentPercentage => TargetQuota > 0 ? Math.Min(100m, (TotalRevenue / TargetQuota) * 100m) : 0m;
    public decimal Commission => TotalRevenue * 0.05m;
}

public class AbandonedCheckoutDashboardViewModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? City { get; set; }
    public string? ShippingAddress { get; set; }
    public string CartItemsJson { get; set; } = "[]";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public bool IsRecovered { get; set; }
    public int? RecoveredOrderId { get; set; }
}
