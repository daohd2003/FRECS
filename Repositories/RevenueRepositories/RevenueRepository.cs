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
                    && (o.Status == OrderStatus.returned))
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
            // Get all returned orders
            var orders = await _context.Orders
                .AsNoTracking()  // Read-only query optimization
                .Include(o => o.Items)
                .Where(o => o.ProviderId == providerId && o.Status == OrderStatus.returned)
                .ToListAsync();

            // Calculate net revenue using SAME logic as RevenueService
            decimal totalGrossRevenue = 0;
            decimal totalPlatformFee = 0;

            foreach (var order in orders)
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

        public async Task<List<TopRevenueItemDto>> GetTopRevenueByProductAsync(Guid providerId, DateTime start, DateTime end, int limit = 5)
        {
            var topProducts = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Images)
                .Where(oi => oi.Order.ProviderId == providerId
                    && oi.Order.Status == OrderStatus.returned
                    && oi.Order.CreatedAt >= start
                    && oi.Order.CreatedAt < end)
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
            var topCustomers = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                    .ThenInclude(c => c.Profile)
                .Where(o => o.ProviderId == providerId
                    && o.Status == OrderStatus.returned
                    && o.CreatedAt >= start
                    && o.CreatedAt < end)
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalSpent = g.Sum(o => o.Subtotal),
                    OrderCount = g.Count(),
                    Customer = g.First().Customer
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(limit)
                .ToListAsync();

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

