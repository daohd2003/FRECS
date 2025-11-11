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

                // Calculate platform fee for each item
                foreach (var item in order.Items)
                {
                    decimal itemRevenue = 0;

                    if (item.TransactionType == TransactionType.rental)
                    {
                        // Rental: DailyRate × RentalDays × Quantity
                        itemRevenue = item.DailyRate * (item.RentalDays ?? 0) * item.Quantity;
                        totalPlatformFee += itemRevenue * 0.20m; // 20% commission
                    }
                    else if (item.TransactionType == TransactionType.purchase)
                    {
                        // Purchase: DailyRate × Quantity (DailyRate is used as unit price)
                        itemRevenue = item.DailyRate * item.Quantity;
                        totalPlatformFee += itemRevenue * 0.10m; // 10% commission
                    }
                }
            }

            // Net revenue from orders = Gross - Platform Fee
            decimal netOrderRevenue = totalGrossRevenue - totalPlatformFee;

            // Get penalty revenue (all time)
            var penaltyRevenue = await _context.RentalViolations
                .AsNoTracking()  // Read-only query optimization
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId
                    && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED || rv.Status == ViolationStatus.RESOLVED))
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
            // 2. Status is CUSTOMER_ACCEPTED or RESOLVED
            // 3. Created within the period
            var penaltyRevenue = await _context.RentalViolations
                .AsNoTracking()  // Read-only query optimization
                .Include(rv => rv.OrderItem)
                    .ThenInclude(oi => oi.Order)
                .Where(rv => rv.OrderItem.Order.ProviderId == providerId
                    && (rv.Status == ViolationStatus.CUSTOMER_ACCEPTED || rv.Status == ViolationStatus.RESOLVED)
                    && rv.CreatedAt >= start
                    && rv.CreatedAt < end)
                .SumAsync(rv => rv.PenaltyAmount);

            return penaltyRevenue;
        }
    }
}

