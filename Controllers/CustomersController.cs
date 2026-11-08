using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Constants;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.ViewModels.Customers;

namespace Stockify.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Customers
    public async Task<IActionResult> Index(string? search, string? filter)
    {
        var query = _context.Customers
            .Include(c => c.Orders)
            .AsNoTracking();

        // High-level statistics
        var totalCustomers = await _context.Customers.CountAsync();
        var activeCustomers = await _context.Customers.CountAsync(c => c.Orders.Any());
        var leadsOnly = await _context.Customers.CountAsync(c => !c.Orders.Any());
        var totalRevenue = await _context.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        ViewBag.TotalCustomers = totalCustomers;
        ViewBag.ActiveCustomers = activeCustomers;
        ViewBag.LeadsOnly = leadsOnly;
        ViewBag.TotalRevenue = totalRevenue;

        // Apply Filter: 'all', 'purchased' (customers with orders), 'leads' (customers without orders)
        var cleanFilter = filter?.Trim().ToLower() ?? "all";
        if (cleanFilter == "purchased" || cleanFilter == "orders")
        {
            query = query.Where(c => c.Orders.Any());
        }
        else if (cleanFilter == "leads" || cleanFilter == "no-orders")
        {
            query = query.Where(c => !c.Orders.Any());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.FullName.ToLower().Contains(s)
                                  || c.Email.ToLower().Contains(s)
                                  || (c.PhoneNumber != null && c.PhoneNumber.Contains(s))
                                  || (c.Address != null && c.Address.ToLower().Contains(s)));
            ViewBag.CurrentSearch = search;
        }

        ViewBag.CurrentFilter = cleanFilter;

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerViewModel
            {
                Id = c.Id,
                FullName = c.FullName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                Address = c.Address,
                OrdersCount = c.Orders.Count,
                TotalSpent = c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return View(customers);
    }

    // GET: Customers/ExportToExcel
    [HttpGet]
    public async Task<IActionResult> ExportToExcel(string? search, string? filter)
    {
        var query = _context.Customers
            .Include(c => c.Orders)
            .AsNoTracking();

        var cleanFilter = filter?.Trim().ToLower() ?? "all";
        if (cleanFilter == "purchased" || cleanFilter == "orders")
        {
            query = query.Where(c => c.Orders.Any());
        }
        else if (cleanFilter == "leads" || cleanFilter == "no-orders")
        {
            query = query.Where(c => !c.Orders.Any());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.FullName.ToLower().Contains(s)
                                  || c.Email.ToLower().Contains(s)
                                  || (c.PhoneNumber != null && c.PhoneNumber.Contains(s))
                                  || (c.Address != null && c.Address.ToLower().Contains(s)));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.FullName,
                c.Email,
                PhoneNumber = c.PhoneNumber ?? "-",
                Address = c.Address ?? "-",
                OrdersCount = c.Orders.Count,
                TotalSpent = c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                c.CreatedAt,
                IsBuyer = c.Orders.Any()
            })
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ROVEN Customers");

        // Header / Brand block
        worksheet.Cell(1, 1).Value = "ROVEN HAUTE PARFUMERIE - PATRONS & CUSTOMERS DIRECTORY";
        worksheet.Range(1, 1, 1, 9).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#630000");
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 28;

        var filterLabel = cleanFilter switch
        {
            "purchased" => "Active Buyers Only (عملاء قاموا بالشراء فقط)",
            "leads" => "Offers & Leads Only (مشتركي العروض بدون طلبات فقط)",
            _ => "All Registered Patrons & Leads (جميع العملاء والمسجلين)"
        };

        worksheet.Cell(2, 1).Value = $"Export Date: {DateTime.UtcNow.ToEgyptTimeString("yyyy-MM-dd HH:mm:ss")} (Cairo Time) | Filter: {filterLabel} | Total: {customers.Count}";
        worksheet.Range(2, 1, 2, 9).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 9;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6c6358");
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(2).Height = 18;

        // Table Column Headers
        string[] headers = { "ID", "Full Name", "Customer Type", "Email Address", "Phone Number", "Address / Location", "Total Orders", "Total Spent (EGP)", "Registration Date" };
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

        // Data Rows
        int rowIdx = 5;
        foreach (var c in customers)
        {
            worksheet.Cell(rowIdx, 1).Value = c.Id;
            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 2).Value = c.FullName;
            worksheet.Cell(rowIdx, 2).Style.Font.Bold = true;

            worksheet.Cell(rowIdx, 3).Value = c.IsBuyer ? "مشتري لديه طلبات (Buyer)" : "مشترك عروض فقط (Lead)";
            worksheet.Cell(rowIdx, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(rowIdx, 3).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 3).Style.Font.FontColor = c.IsBuyer ? XLColor.FromHtml("#137333") : XLColor.FromHtml("#b06000");

            worksheet.Cell(rowIdx, 4).Value = (c.Email != null && c.Email.StartsWith("whatsapp_") && c.Email.EndsWith("@roven.vip"))
                ? "مسجل عبر واتساب (WhatsApp Lead)"
                : c.Email;

            worksheet.Cell(rowIdx, 5).Value = c.PhoneNumber;
            worksheet.Cell(rowIdx, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 6).Value = c.Address;

            worksheet.Cell(rowIdx, 7).Value = c.OrdersCount;
            worksheet.Cell(rowIdx, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 8).Value = c.TotalSpent;
            worksheet.Cell(rowIdx, 8).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            worksheet.Cell(rowIdx, 8).Style.Font.Bold = true;

            worksheet.Cell(rowIdx, 9).Value = c.CreatedAt.ToEgyptTime().ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(rowIdx, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rowRange = worksheet.Range(rowIdx, 1, rowIdx, 9);
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
        if (customers.Any())
        {
            worksheet.Cell(rowIdx, 1).Value = "TOTAL";
            worksheet.Range(rowIdx, 1, rowIdx, 6).Merge();
            worksheet.Cell(rowIdx, 1).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(rowIdx, 7).Value = customers.Sum(c => c.OrdersCount);
            worksheet.Cell(rowIdx, 7).Style.Font.Bold = true;
            worksheet.Cell(rowIdx, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(rowIdx, 8).Value = customers.Sum(c => c.TotalSpent);
            worksheet.Cell(rowIdx, 8).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            worksheet.Cell(rowIdx, 8).Style.Font.Bold = true;

            var totalRange = worksheet.Range(rowIdx, 1, rowIdx, 9);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEBDD");
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            worksheet.Row(rowIdx).Height = 24;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var fileName = $"ROVEN_Customers_{DateTime.UtcNow.ToEgyptTime():yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Customers/Create
    public IActionResult Create()
    {
        return View(new CustomerViewModel());
    }

    // POST: Customers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "A customer with this email address already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = new Customer
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = model.PhoneNumber?.Trim(),
            Address = model.Address?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Customer '{customer.FullName}' registered successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Customers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        var model = new CustomerViewModel
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber,
            Address = customer.Address,
            CreatedAt = customer.CreatedAt
        };

        return View(model);
    }

    // POST: Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        if (await _context.Customers.AnyAsync(c => c.Id != id && c.Email.ToLower() == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Another customer already uses this email address.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        customer.FullName = model.FullName.Trim();
        customer.Email = normalizedEmail;
        customer.PhoneNumber = model.PhoneNumber?.Trim();
        customer.Address = model.Address?.Trim();

        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Customer '{customer.FullName}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Customers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Orders)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }

    // POST: Customers/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        // Business Rule Protection: Prevent deleting customer with existing sales orders
        var hasOrders = await _context.Orders.AnyAsync(o => o.CustomerId == id);
        if (hasOrders)
        {
            TempData["ErrorMessage"] = $"Cannot delete customer '{customer.FullName}' because they have active sales orders on record. Remove their orders first or keep their profile for financial audit.";
            return RedirectToAction(nameof(Index));
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Customer '{customer.FullName}' has been permanently deleted.";
        return RedirectToAction(nameof(Index));
    }
}
