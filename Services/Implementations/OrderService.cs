using System.Data;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.Models.Entities;
using Stockify.Models.Enums;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request == null || request.Items == null || !request.Items.Any())
        {
            return new CreateOrderResult
            {
                Success = false,
                ErrorMessage = "لا توجد منتجات محددة في هذا الطلب."
            };
        }

        // Consolidate duplicate products in the request
        var consolidatedItems = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new CreateOrderItemRequest
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var item in consolidatedItems)
        {
            if (item.Quantity <= 0)
            {
                return new CreateOrderResult
                {
                    Success = false,
                    ErrorMessage = "يجب أن تكون كمية كل منتج 1 على الأقل.",
                    OutOfStockProductId = item.ProductId
                };
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                // 1. Fetch active catalog products using AsNoTracking (so change tracker doesn't conflict with direct SQL updates)
                var productIds = consolidatedItems.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                foreach (var item in consolidatedItems)
                {
                    if (!products.TryGetValue(item.ProductId, out var prod) || !prod.IsActive)
                    {
                        await transaction.RollbackAsync();
                        return new CreateOrderResult
                        {
                            Success = false,
                            ErrorMessage = $"المنتج #{item.ProductId} غير متوفر في الكتالوج الحالي.",
                            OutOfStockProductId = item.ProductId
                        };
                    }
                }

                // 2. ATOMIC STOCK DEDUCTION (Concurrency & Race Condition Prevention)
                // Atomic condition: UPDATE Products SET StockQuantity = StockQuantity - @Quantity WHERE Id = @Id AND StockQuantity >= @Quantity
                // If rows affected == 0, another concurrent order took the last flacon or stock is insufficient!
                foreach (var item in consolidatedItems)
                {
                    var prod = products[item.ProductId];

                    int rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE [Products] SET [StockQuantity] = [StockQuantity] - {item.Quantity} WHERE [Id] = {item.ProductId} AND [StockQuantity] >= {item.Quantity};"
                    );

                    if (rowsAffected == 0)
                    {
                        // Race condition caught! The available stock is less than requested quantity.
                        await transaction.RollbackAsync();

                        var currentStock = await _context.Products
                            .AsNoTracking()
                            .Where(p => p.Id == item.ProductId)
                            .Select(p => p.StockQuantity)
                            .FirstOrDefaultAsync();

                        _logger.LogWarning(
                            "Concurrent order conflict / Insufficient stock detected for Product ID {ProductId} ('{ProductName}'). Requested: {Requested}, Available: {Available}.",
                            prod.Id, prod.Name, item.Quantity, currentStock);

                        return new CreateOrderResult
                        {
                            Success = false,
                            ErrorMessage = $"عذراً، نفد المخزون المتاح للمنتج '{prod.Name}'. الكمية المطلوبة ({item.Quantity}) لم تعد متوفرة حالياً (المتبقي الفعلي في الأتيلييه: {currentStock} قطعة).",
                            OutOfStockProductId = prod.Id,
                            OutOfStockProductName = prod.Name,
                            AvailableStock = currentStock
                        };
                    }
                }

                // 3. Resolve Customer (Existing, by Phone/Email, or Register New Guest)
                int customerId;
                Customer? resolvedCustomer = null;

                var phoneClean = (request.CustomerPhone ?? "").Trim();
                var emailClean = (request.CustomerEmail ?? "").Trim();

                if (request.CustomerId.HasValue && request.CustomerId.Value > 0)
                {
                    resolvedCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value);
                    if (resolvedCustomer != null)
                    {
                        customerId = resolvedCustomer.Id;

                        // Keep existing customer record updated if new details were provided
                        if (!string.IsNullOrWhiteSpace(request.CustomerName)) resolvedCustomer.FullName = request.CustomerName.Trim();
                        if (!string.IsNullOrWhiteSpace(phoneClean)) resolvedCustomer.PhoneNumber = phoneClean;
                        if (!string.IsNullOrWhiteSpace(emailClean)) resolvedCustomer.Email = emailClean;
                        if (!string.IsNullOrWhiteSpace(request.ShippingAddress) || !string.IsNullOrWhiteSpace(request.City))
                        {
                            var fullAddr = $"{request.ShippingAddress?.Trim()}, {request.City?.Trim()}".Trim().Trim(',');
                            if (!string.IsNullOrWhiteSpace(fullAddr)) resolvedCustomer.Address = fullAddr;
                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        var defaultCust = await _context.Customers.FirstOrDefaultAsync();
                        customerId = defaultCust?.Id ?? throw new InvalidOperationException("No valid customer registered in system.");
                        resolvedCustomer = defaultCust;
                    }
                }
                else
                {
                    resolvedCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => (!string.IsNullOrEmpty(phoneClean) && c.PhoneNumber == phoneClean) ||
                                                  (!string.IsNullOrEmpty(emailClean) && c.Email.ToLower() == emailClean.ToLower()));

                    if (resolvedCustomer != null)
                    {
                        customerId = resolvedCustomer.Id;

                        // Update existing customer profile with any new information provided
                        if (!string.IsNullOrWhiteSpace(request.CustomerName)) resolvedCustomer.FullName = request.CustomerName.Trim();
                        if (!string.IsNullOrWhiteSpace(phoneClean)) resolvedCustomer.PhoneNumber = phoneClean;
                        if (!string.IsNullOrWhiteSpace(emailClean)) resolvedCustomer.Email = emailClean;
                        if (!string.IsNullOrWhiteSpace(request.ShippingAddress) || !string.IsNullOrWhiteSpace(request.City))
                        {
                            var fullAddr = $"{request.ShippingAddress?.Trim()}, {request.City?.Trim()}".Trim().Trim(',');
                            if (!string.IsNullOrWhiteSpace(fullAddr)) resolvedCustomer.Address = fullAddr;
                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        var fallbackEmail = !string.IsNullOrWhiteSpace(emailClean)
                            ? emailClean
                            : (!string.IsNullOrWhiteSpace(phoneClean) ? $"patron_{phoneClean.Replace(" ", "").Replace("+", "")}@roven.eg" : $"patron_{Guid.NewGuid().ToString("N")[..8]}@roven.eg");

                        var newCustomer = new Customer
                        {
                            FullName = string.IsNullOrWhiteSpace(request.CustomerName) ? "ROVEN Patron" : request.CustomerName.Trim(),
                            PhoneNumber = phoneClean,
                            Email = fallbackEmail,
                            Address = $"{request.ShippingAddress?.Trim()}, {request.City?.Trim()}".Trim().Trim(','),
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Customers.Add(newCustomer);
                        await _context.SaveChangesAsync();
                        customerId = newCustomer.Id;
                        resolvedCustomer = newCustomer;
                    }
                }

                // Fill fallback names if not provided in request
                if (string.IsNullOrWhiteSpace(request.CustomerName) && resolvedCustomer != null)
                {
                    request.CustomerName = resolvedCustomer.FullName;
                }
                if (string.IsNullOrWhiteSpace(request.CustomerPhone) && resolvedCustomer != null)
                {
                    request.CustomerPhone = resolvedCustomer.PhoneNumber;
                }
                if (string.IsNullOrWhiteSpace(request.CustomerEmail) && resolvedCustomer != null)
                {
                    request.CustomerEmail = resolvedCustomer.Email;
                }

                // 4. Calculate Server-Verified Pricing (Zero VAT / All-Inclusive Luxury Pricing)
                // Check if any product in order is currently part of an active HomeOffer
                var prodIds = consolidatedItems.Select(i => i.ProductId).ToList();
                var activeOffers = await _context.HomeOffers
                    .Where(o => prodIds.Contains(o.ProductId) && o.IsActive)
                    .ToDictionaryAsync(o => o.ProductId, o => o.OfferPrice);

                decimal subtotal = 0;
                foreach (var item in consolidatedItems)
                {
                    var unitPrice = (activeOffers.TryGetValue(item.ProductId, out var op) && op > 0)
                        ? op
                        : products[item.ProductId].Price;
                    subtotal += unitPrice * item.Quantity;
                }

                // Apply Coupon if supplied
                decimal discountAmount = 0;
                int? appliedCouponId = null;
                string? appliedCouponCode = null;

                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    var codeClean = request.CouponCode.Trim().ToUpper();
                    var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == codeClean);
                    if (coupon != null && coupon.IsActive)
                    {
                        var now = DateTime.UtcNow;
                        bool validDate = (!coupon.StartDate.HasValue || coupon.StartDate <= now) &&
                                         (!coupon.ExpiryDate.HasValue || coupon.ExpiryDate >= now);
                        bool validUsage = !coupon.MaxUsageCount.HasValue || coupon.TimesUsed < coupon.MaxUsageCount.Value;
                        bool validMin = !coupon.MinimumOrderAmount.HasValue || subtotal >= coupon.MinimumOrderAmount.Value;

                        if (validDate && validUsage && validMin)
                        {
                            if (coupon.DiscountType == CouponDiscountType.Percentage)
                            {
                                discountAmount = Math.Round((subtotal * coupon.DiscountValue) / 100m, 2);
                                if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
                                {
                                    discountAmount = coupon.MaxDiscountAmount.Value;
                                }
                            }
                            else
                            {
                                discountAmount = Math.Min(subtotal, coupon.DiscountValue);
                            }

                            appliedCouponId = coupon.Id;
                            appliedCouponCode = coupon.Code;
                            coupon.TimesUsed += 1;
                        }
                    }
                }

                decimal grandTotal = Math.Max(0m, subtotal - discountAmount + request.ShippingFee);

                // 5. Generate Unique Order Reference
                var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    OrderDate = DateTime.UtcNow,
                    Status = request.Status,
                    OrderSource = request.OrderSource,
                    CustomerId = customerId,
                    CustomerName = request.CustomerName?.Trim(),
                    CustomerPhone = request.CustomerPhone?.Trim(),
                    CustomerEmail = request.CustomerEmail?.Trim(),
                    ShippingAddress = request.ShippingAddress?.Trim(),
                    City = request.City?.Trim(),
                    OrderNotes = request.OrderNotes?.Trim(),
                    PaymentMethod = request.PaymentMethod ?? "Cash on Delivery",
                    CouponId = appliedCouponId,
                    CouponCode = appliedCouponCode,
                    DiscountAmount = discountAmount,
                    ShippingFee = request.ShippingFee,
                    TotalAmount = grandTotal
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 6. Create Order Items & Ledger Audit Trail (InventoryTransaction)
                foreach (var item in consolidatedItems)
                {
                    var prod = products[item.ProductId];
                    var itemUnitPrice = (activeOffers.TryGetValue(item.ProductId, out var op) && op > 0)
                        ? op
                        : prod.Price;

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = prod.Id,
                        Quantity = item.Quantity,
                        UnitPrice = itemUnitPrice
                    };
                    _context.OrderItems.Add(orderItem);

                    var auditNote = request.OrderSource == OrderSource.Website
                        ? $"Website Order #{order.OrderNumber} by {order.CustomerName ?? "Online Patron"}"
                        : $"POS In-Store Sale #{order.OrderNumber}";

                    var audit = new InventoryTransaction
                    {
                        ProductId = prod.Id,
                        Quantity = item.Quantity,
                        TransactionType = TransactionType.StockOut,
                        TransactionDate = DateTime.UtcNow,
                        Notes = auditNote,
                        OrderId = order.Id,
                        UserId = request.UserId
                    };
                    _context.InventoryTransactions.Add(audit);
                }

                await _context.SaveChangesAsync();

                // 7. Commit Transaction
                await transaction.CommitAsync();

                _logger.LogInformation("Order {OrderNumber} successfully created via {OrderSource}. Total: {TotalAmount} EGP.",
                    order.OrderNumber, order.OrderSource, order.TotalAmount);

                return new CreateOrderResult
                {
                    Success = true,
                    Order = order
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error in atomic OrderService.CreateOrderAsync.");
                throw;
            }
        });
    }

    public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            return null;

        var clean = orderNumber.Trim().ToLower();

        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OrderNumber.ToLower() == clean);
    }
}
