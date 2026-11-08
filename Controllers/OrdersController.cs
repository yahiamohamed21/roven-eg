using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Models.DTOs;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;

namespace Stockify.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOrderService _orderService;

    public OrdersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOrderService orderService)
    {
        _context = context;
        _userManager = userManager;
        _orderService = orderService;
    }

    // GET: Orders
    public async Task<IActionResult> Index(string? search, OrderStatus? status, OrderSource? source, DateTime? date)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .AsNoTracking();

        // 1. Calculate Summary Statistics across all orders
        ViewBag.TotalOrders = await _context.Orders.CountAsync();
        ViewBag.CompletedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Completed);
        ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing);
        ViewBag.CancelledOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled);
        ViewBag.TotalSalesRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        // E-Commerce Specific Stats
        ViewBag.WebsiteOrders = await _context.Orders.CountAsync(o => o.OrderSource == OrderSource.Website);
        ViewBag.PendingWebsiteOrders = await _context.Orders.CountAsync(o => o.OrderSource == OrderSource.Website && o.Status == OrderStatus.Pending);
        ViewBag.ManualOrders = await _context.Orders.CountAsync(o => o.OrderSource == OrderSource.Manual);
        ViewBag.WebsiteRevenue = await _context.Orders
            .Where(o => o.OrderSource == OrderSource.Website && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        ViewBag.CurrentSearch = search;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSource = source;
        ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");

        // 2. Apply Filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(s)
                                  || (o.Customer != null && (o.Customer.FullName.Contains(s) || o.Customer.Email.Contains(s) || (o.Customer.PhoneNumber != null && o.Customer.PhoneNumber.Contains(s))))
                                  || (o.CustomerName != null && o.CustomerName.Contains(s))
                                  || (o.CustomerPhone != null && o.CustomerPhone.Contains(s))
                                  || (o.City != null && o.City.Contains(s)));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (source.HasValue)
        {
            query = query.Where(o => o.OrderSource == source.Value);
        }

        if (date.HasValue)
        {
            var targetLocal = date.Value.Date;
            var startUtc = targetLocal.ToUtcFromEgypt();
            var endUtc = targetLocal.AddDays(1).ToUtcFromEgypt();
            query = query.Where(o => o.OrderDate >= startUtc && o.OrderDate < endUtc);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    // GET: Orders/ExportToExcel
    [HttpGet]
    public async Task<IActionResult> ExportToExcel(string? search, OrderStatus? status, OrderSource? source, DateTime? date)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(s)
                                  || (o.Customer != null && (o.Customer.FullName.Contains(s) || o.Customer.Email.Contains(s) || (o.Customer.PhoneNumber != null && o.Customer.PhoneNumber.Contains(s))))
                                  || (o.CustomerName != null && o.CustomerName.Contains(s))
                                  || (o.CustomerPhone != null && o.CustomerPhone.Contains(s))
                                  || (o.City != null && o.City.Contains(s)));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (source.HasValue)
        {
            query = query.Where(o => o.OrderSource == source.Value);
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(o => o.OrderDate.Date == targetDate);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ROVEN Orders");

        // Title / Brand block
        worksheet.Cell(1, 1).Value = "ROVEN HAUTE PARFUMERIE - OMNICHANNEL SALES ORDERS";
        worksheet.Range(1, 1, 1, 11).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 28;

        worksheet.Cell(2, 1).Value = $"Export Date: {DateTime.UtcNow.ToEgyptTimeString("yyyy-MM-dd HH:mm:ss")} (Cairo Time) | Total Orders: {orders.Count}";
        worksheet.Range(2, 1, 2, 11).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 9;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6c6358");
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(2).Height = 18;

        // Headers on row 4
        string[] headers =
        {
            "Order #", "Date & Time", "Source", "Customer Name", "Phone",
            "City / Address", "Line Items", "Total Units", "Payment Method", "Status", "Total Amount (EGP)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 10;
            cell.Style.Font.FontColor = XLColor.FromHtml("#EDEBDD");
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#630000");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#810100");
        }
        worksheet.Row(4).Height = 26;

        // Rows
        int rowIdx = 5;
        foreach (var o in orders)
        {
            var custName = !string.IsNullOrWhiteSpace(o.CustomerName) ? o.CustomerName : (o.Customer?.FullName ?? "Direct Guest");
            var custPhone = !string.IsNullOrWhiteSpace(o.CustomerPhone) ? o.CustomerPhone : (o.Customer?.PhoneNumber ?? "-");
            var destination = !string.IsNullOrWhiteSpace(o.City) ? $"{o.City} - {o.ShippingAddress}" : (o.Customer?.Address ?? "Boutique Counter");
            var totalUnits = o.OrderItems.Sum(i => i.Quantity);

            worksheet.Cell(rowIdx, 1).Value = o.OrderNumber;
            worksheet.Cell(rowIdx, 1).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 2).Value = o.OrderDate.ToEgyptTime().ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(rowIdx, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 3).Value = o.OrderSource == OrderSource.Website ? "Website / E-Commerce" : "POS / Boutique";
            worksheet.Cell(rowIdx, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 4).Value = custName;

            worksheet.Cell(rowIdx, 5).Value = custPhone;
            worksheet.Cell(rowIdx, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 6).Value = destination;

            worksheet.Cell(rowIdx, 7).Value = o.OrderItems.Count;
            worksheet.Cell(rowIdx, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 8).Value = totalUnits;
            worksheet.Cell(rowIdx, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 9).Value = o.PaymentMethod.ToString();
            worksheet.Cell(rowIdx, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 10).Value = o.Status.ToString();
            worksheet.Cell(rowIdx, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 11).Value = o.TotalAmount;
            worksheet.Cell(rowIdx, 11).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            worksheet.Cell(rowIdx, 11).Style.Font.Bold = true;

            var rowRange = worksheet.Range(rowIdx, 1, rowIdx, 11);
            rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#e8e3d5");

            if (rowIdx % 2 == 0)
            {
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF9F5");
            }
            worksheet.Row(rowIdx).Height = 22;
            rowIdx++;
        }

        // Totals row
        if (orders.Any())
        {
            worksheet.Cell(rowIdx, 1).Value = "TOTAL (NON-CANCELLED)";
            worksheet.Range(rowIdx, 1, rowIdx, 7).Merge();
            worksheet.Cell(rowIdx, 1).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(rowIdx, 8).Value = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.OrderItems.Sum(i => i.Quantity));
            worksheet.Cell(rowIdx, 8).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 11).Value = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
            worksheet.Cell(rowIdx, 11).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            worksheet.Cell(rowIdx, 11).Style.Font.Bold = true;

            var totalRange = worksheet.Range(rowIdx, 1, rowIdx, 11);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            worksheet.Row(rowIdx).Height = 24;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var fileName = $"ROVEN_Orders_{DateTime.UtcNow.ToEgyptTime():yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Orders/Create
    public async Task<IActionResult> Create(int? customerId)
    {
        ViewBag.Customers = await _context.Customers.OrderBy(c => c.FullName).ToListAsync();
        ViewBag.SelectedCustomerId = customerId;

        // Take active products with categories for initial instant load
        ViewBag.Products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Take(50)
            .ToListAsync();

        return View();
    }

    // GET: Orders/SearchProducts
    [HttpGet]
    public async Task<IActionResult> SearchProducts(string? q)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        var products = await query
            .OrderBy(p => p.Name)
            .Take(50)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                sku = p.Sku,
                price = p.Price,
                stock = p.StockQuantity,
                category = p.Category != null ? p.Category.Name : "General"
            })
            .ToListAsync();

        return Json(products);
    }

    // POST: Orders/CreateOrder (Delegated to Unified IOrderService with Atomic Database Concurrency Locking)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto request)
    {
        if (request == null || request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { success = false, message = "لم يتم تحديد أي عطور في هذا الطلب." });
        }

        var orderRequest = new CreateOrderRequest
        {
            OrderSource = OrderSource.Manual,
            Status = OrderStatus.Completed,
            CustomerId = request.CustomerId > 0 ? request.CustomerId : null,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            City = request.City,
            ShippingAddress = request.ShippingAddress,
            PaymentMethod = request.PaymentMethod ?? "Cash on Delivery",
            OrderNotes = request.Notes,
            CouponCode = request.CouponCode,
            UserId = _userManager.GetUserId(User),
            Items = request.Items.Select(i => new CreateOrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        var result = await _orderService.CreateOrderAsync(orderRequest);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                message = result.ErrorMessage,
                outOfStockProductId = result.OutOfStockProductId
            });
        }

        var order = result.Order!;
        decimal subtotal = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);

        return Json(new
        {
            success = true,
            orderId = order.Id,
            orderNumber = order.OrderNumber,
            subtotal = subtotal,
            tax = 0m,
            totalAmount = order.TotalAmount,
            message = $"تم تسجيل الطلب #{order.OrderNumber} بنجاح وخصم المخزون بشكل ذري آمن!"
        });
    }

    // GET: Orders/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    // POST: Orders/UpdateStatus/5 (Lifecycle Transitions with Stock Restoration on Cancellation)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == newStatus)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            var previousStatus = order.Status;
            var currentUserId = _userManager.GetUserId(User);

            // Business Rule: If an active order is Cancelled, restore all inventory back to stock!
            if (previousStatus != OrderStatus.Cancelled && newStatus == OrderStatus.Cancelled)
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockQuantity += item.Quantity;
                        _context.Products.Update(item.Product);

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            TransactionType = TransactionType.Return,
                            TransactionDate = DateTime.UtcNow,
                            Notes = $"Restocked from cancelled order #{order.OrderNumber}",
                            UserId = currentUserId
                        });
                    }
                }
            }
            // If transitioning FROM Cancelled back to Active status, re-check and deduct inventory
            else if (previousStatus == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        if (item.Product.StockQuantity < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            TempData["ErrorMessage"] = $"Cannot reactivate order: Insufficient stock for '{item.Product.Name}'. Required: {item.Quantity}, Available: {item.Product.StockQuantity}.";
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        item.Product.StockQuantity -= item.Quantity;
                        _context.Products.Update(item.Product);

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            TransactionType = TransactionType.StockOut,
                            TransactionDate = DateTime.UtcNow,
                            Notes = $"Deducted stock on reactivating order #{order.OrderNumber}",
                            UserId = currentUserId
                        });
                    }
                }
            }

            order.Status = newStatus;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status successfully updated to '{newStatus}'!";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Failed to update order status: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult MarkNotificationsAsRead([FromBody] MarkNotificationReadRequest? request)
    {
        var timestamp = request?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Response.Cookies.Append("roven_notifs_read_at", timestamp.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        if (request?.OrderId.HasValue == true)
        {
            var existing = Request.Cookies["roven_read_orders"] ?? "";
            var ids = existing.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var orderIdStr = request.OrderId.Value.ToString();
            if (!ids.Contains(orderIdStr))
            {
                ids.Add(orderIdStr);
            }
            Response.Cookies.Append("roven_read_orders", string.Join(",", ids), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        return Json(new { success = true, timestamp });
    }
}

public class MarkNotificationReadRequest
{
    public long? Timestamp { get; set; }
    public int? OrderId { get; set; }
}
