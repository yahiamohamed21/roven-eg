using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Stockify.Models.Enums;

namespace Stockify.ViewModels.Inventory;

public class InventoryItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue => CurrentStock * UnitPrice;
    public DateTime? LastMovementDate { get; set; }

    public bool IsOutOfStock => CurrentStock <= 0;
    public bool IsLowStock => CurrentStock > 0 && CurrentStock <= ReorderLevel;
    public bool IsSufficientStock => CurrentStock > ReorderLevel;
}

public class StockInViewModel
{
    [Required(ErrorMessage = "Please select a product.")]
    [Display(Name = "Product")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, 1000000, ErrorMessage = "Quantity must be at least 1 unit.")]
    [Display(Name = "Quantity Received")]
    public int Quantity { get; set; }

    [StringLength(100, ErrorMessage = "Supplier reference cannot exceed 100 characters.")]
    [Display(Name = "Supplier Name / Invoice Reference")]
    public string? SupplierReference { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Received Date")]
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    [Display(Name = "Batch Notes / Details")]
    public string? Notes { get; set; }

    public List<SelectListItem> ProductsList { get; set; } = new();
}

public class StockAdjustmentViewModel
{
    [Required(ErrorMessage = "Please select a product.")]
    [Display(Name = "Product")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, 1000000, ErrorMessage = "Quantity must be at least 1 unit.")]
    [Display(Name = "Quantity to Adjust")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Please specify the adjustment type.")]
    [Display(Name = "Action Type")]
    public TransactionType AdjustmentType { get; set; } = TransactionType.Adjustment;

    [Required(ErrorMessage = "Reason is required for inventory audit trail.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    [Display(Name = "Reason for Adjustment / Note")]
    public string Reason { get; set; } = string.Empty;

    public List<SelectListItem> ProductsList { get; set; } = new();
}

public class LowStockItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
    public int Deficit => Math.Max(0, ReorderLevel - CurrentStock);
    public bool IsOutOfStock => CurrentStock <= 0;
}

public class InventoryTransactionViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
    public string PerformerName { get; set; } = string.Empty;
}
