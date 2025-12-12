using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Repositories.RevenueRepositories
{
    public class RevenueRepository : IRevenueRepository
    {
        private readonly ShareItDbContext _context;

        public RevenueRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetOrdersInPeriodAsync(Guid providerId, DateTime start, DateTime end)
        {
            return await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Include(o => o.Items)  // Include OrderItems for commission calculation
                .Where(o => o.ProviderId == providerId
                    && o.CreatedAt >= start
                    && o.CreatedAt < end
                    && (o.Status == OrderStatus.returned 
                        || (o.Status == OrderStatus.in_use && o.Items.Any(i => i.TransactionType == TransactionType.purchase))))
                .ToListAsync();
        }

        public async Task<List<Order>> GetAllOrdersInPeriodAsync(Guid providerId, DateTime start, DateTime end)
        {
            return await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Include(o => o.Items)  // Include OrderItems for status breakdown
                .Where(o => o.ProviderId == providerId
                    && o.CreatedAt >= start
                    && o.CreatedAt < end)
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersByProviderIdAsync(Guid providerId)
        {
            return await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Where(o => o.ProviderId == providerId)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalEarningsAsync(Guid providerId)
        {
            // Get orders for this provider:
            // - Rental orders: status must be 'returned'
            // - Purchase orders: status can be 'in_use' or 'returned' (when customer receives the item, status is in_use)
            var orders = await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Include(o => o.Items)
                .Where(o => o.ProviderId == providerId 
                    && (o.Status == OrderStatus.returned 
                        || (o.Status == OrderStatus.in_use && o.Items.Any(i => i.TransactionType == TransactionType.purchase))))
                .ToListAsync();

            // Calculate net revenue using SAME logic as RevenueService
            decimal totalGrossRevenue = 0;
            decimal totalPlatformFee = 0;

            foreach (var order in orders)
            {
                // Check if this order should be counted:
                // - If order has rental items, only count when status is 'returned'
                // - If order has purchase items, count when status is 'in_use' or 'returned'
                bool hasRentalItems = order.Items.Any(i => i.TransactionType == TransactionType.rental);
                bool hasPurchaseItems = order.Items.Any(i => i.TransactionType == TransactionType.purchase);
                
                bool shouldCount = false;
                if (hasRentalItems && !hasPurchaseItems)
                {
                    // Pure rental order: only count when returned
                    shouldCount = order.Status == OrderStatus.returned;
                }
                else if (hasPurchaseItems && !hasRentalItems)
                {
                    // Pure purchase order: count when in_use or returned
                    shouldCount = order.Status == OrderStatus.in_use || order.Status == OrderStatus.returned;
                }
                else
                {
                    // Mixed order: count when returned (rental items require return)
                    shouldCount = order.Status == OrderStatus.returned;
                }

                if (shouldCount)
                {
                    // Add gross revenue (Subtotal excludes deposit)
                    totalGrossRevenue += order.Subtotal;

                    // Calculate platform fee using commission amount saved at order creation time
                    foreach (var item in order.Items)
                    {
                        // Use the commission amount that was calculated and saved when the order was created
                        // This ensures historical accuracy regardless of current commission rate changes
                        totalPlatformFee += item.CommissionAmount;
                    }
                }
            }

            // Net revenue from orders = Gross - Platform Fee
            decimal netOrderRevenue = totalGrossRevenue - totalPlatformFee;

            // Get penalty revenue (all time) - includes RESOLVED_BY_ADMIN
            var penaltyRevenue = await _context.RentalViolations
                .AsNoTracking()  // Read-only query optimization
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId
                    && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED 
                        || rv.Status == ViolationStatus.RESOLVED 
                        || rv.Status == ViolationStatus.RESOLVED_BY_ADMIN))
                .SumAsync(rv => rv.PenaltyAmount);

            // Total Earnings = Net from Orders + Penalties
            return netOrderRevenue + penaltyRevenue;
        }

        public async Task<decimal> GetPendingAmountAsync(Guid providerId)
        {
            return await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Where(o => o.ProviderId == providerId && o.Status == OrderStatus.returned)
                .SumAsync(o => o.Subtotal);  // Use Subtotal (excludes deposit)
        }

        public async Task<decimal> GetPenaltyRevenueInPeriodAsync(Guid providerId, DateTime start, DateTime end)
        {
            // Get all violations where:
            // 1. OrderItem belongs to this provider
            // 2. Status is CUSTOMER_ACCEPTED, RESOLVED, or RESOLVED_BY_ADMIN
            // 3. Updated within the period (when the violation was resolved)
            var penaltyRevenue = await _context.RentalViolations
                .AsNoTracking()  // Read-only query optimization
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId
                    && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED 
                        || rv.Status == ViolationStatus.RESOLVED 
                        || rv.Status == ViolationStatus.RESOLVED_BY_ADMIN)
                    && rv.UpdatedAt.HasValue
                    && rv.UpdatedAt >= start
                    && rv.UpdatedAt < end)
                .SumAsync(rv => rv.PenaltyAmount);

            return penaltyRevenue;
        }

        public async Task<List<TopRevenueItemDto>> GetTopRevenueByProductAsync(Guid providerId, DateTime start, DateTime end, int limit = 5, TransactionType? transactionType = null)
        {
            // Logic:
            // - Rental orders: only count when status is 'returned'
            // - Purchase orders: count when status is 'in_use' or 'returned' (when customer receives the item, status is in_use)
            // - Mixed orders: count when status is 'returned' (rental items require return)
            
            // First, get all orders in the period with their items
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.ProviderId == providerId
                    && o.CreatedAt >= start
                    && o.CreatedAt < end
                    && (o.Status == OrderStatus.returned 
                        || o.Status == OrderStatus.in_use))
                .ToListAsync();

            // Filter orders based on the correct logic
            var validOrderIds = new List<Guid>();
            foreach (var order in orders)
            {
                bool hasRentalItems = order.Items.Any(i => i.TransactionType == TransactionType.rental);
                bool hasPurchaseItems = order.Items.Any(i => i.TransactionType == TransactionType.purchase);
                
                bool shouldCount = false;
                if (hasRentalItems && !hasPurchaseItems)
                {
                    // Pure rental order: only count when returned
                    shouldCount = order.Status == OrderStatus.returned;
                }
                else if (hasPurchaseItems && !hasRentalItems)
                {
                    // Pure purchase order: count when in_use or returned
                    shouldCount = order.Status == OrderStatus.in_use || order.Status == OrderStatus.returned;
                }
                else if (hasRentalItems && hasPurchaseItems)
                {
                    // Mixed order: count when returned (rental items require return)
                    shouldCount = order.Status == OrderStatus.returned;
                }

                if (shouldCount)
                {
                    validOrderIds.Add(order.Id);
                }
            }

            // Now query order items from valid orders
            var query = _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Images)
                .Where(oi => validOrderIds.Contains(oi.OrderId));

            // Filter by transaction type if specified
            if (transactionType.HasValue)
            {
                query = query.Where(oi => oi.TransactionType == transactionType.Value);
            }

            var topProducts = await query
                .GroupBy(oi => new { oi.ProductId, oi.TransactionType })
                .Select(g => new
                {
                    ProductId = g.Key.ProductId,
                    TransactionType = g.Key.TransactionType,
                    Revenue = g.Sum(oi => oi.TransactionType == TransactionType.rental 
                        ? oi.DailyRate * (oi.RentalDays ?? 1) * oi.Quantity
                        : oi.DailyRate * oi.Quantity),
                    OrderCount = g.Select(oi => oi.OrderId).Distinct().Count(),
                    Product = g.First().Product
                })
                .OrderByDescending(x => x.Revenue)
                .Take(limit)
                .ToListAsync();

            return topProducts.Select(x => new TopRevenueItemDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product?.Name ?? "Unknown",
                ProductImageUrl = x.Product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl ?? 
                                 x.Product?.Images?.FirstOrDefault()?.ImageUrl ?? string.Empty,
                Revenue = x.Revenue,
                OrderCount = x.OrderCount,
                TransactionType = x.TransactionType == TransactionType.rental ? "rental" : "purchase"
            }).ToList();
        }

        public async Task<List<TopCustomerDto>> GetTopCustomersAsync(Guid providerId, DateTime start, DateTime end, int limit = 5)
        {
            // Load orders that could contribute to revenue:
            // - rental orders counted only when returned
            // - purchase orders counted when in_use or returned
            // - mixed orders counted when returned
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.Customer)
                    .ThenInclude(c => c.Profile)
                .Where(o => o.ProviderId == providerId
                    && o.CreatedAt >= start
                    && o.CreatedAt < end
                    && (o.Status == OrderStatus.returned || o.Status == OrderStatus.in_use))
                .ToListAsync();

            var validOrders = orders
                .Where(order =>
                {
                    var hasRentalItems = order.Items.Any(i => i.TransactionType == TransactionType.rental);
                    var hasPurchaseItems = order.Items.Any(i => i.TransactionType == TransactionType.purchase);

                    if (hasRentalItems && !hasPurchaseItems)
                    {
                        return order.Status == OrderStatus.returned;
                    }

                    if (hasPurchaseItems && !hasRentalItems)
                    {
                        return order.Status == OrderStatus.in_use || order.Status == OrderStatus.returned;
                    }

                    // Mixed order: only count when returned so rental flow is completed
                    return order.Status == OrderStatus.returned;
                })
                .ToList();

            var topCustomers = validOrders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Customer = g.First().Customer,
                    TotalSpent = g.Sum(o => o.Subtotal),
                    OrderCount = g.Count()
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(limit)
                .ToList();

            return topCustomers.Select(x => new TopCustomerDto
            {
                CustomerId = x.CustomerId,
                CustomerName = x.Customer?.Profile?.FullName ?? x.Customer?.Email ?? "Unknown",
                CustomerEmail = x.Customer?.Email ?? string.Empty,
                CustomerAvatarUrl = x.Customer?.Profile?.ProfilePictureUrl,
                TotalSpent = x.TotalSpent,
                OrderCount = x.OrderCount
            }).ToList();
        }
    }
}

