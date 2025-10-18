using BusinessObject.DTOs.CustomerDashboard;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.OrderRepositories;

namespace Services.CustomerDashboardServices
{
    public class CustomerDashboardService : ICustomerDashboardService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly ShareItDbContext _context;

        public CustomerDashboardService(IOrderRepository orderRepo, ShareItDbContext context)
        {
            _orderRepo = orderRepo;
            _context = context;
        }

        private async Task<List<Order>> GetCustomerOrdersWithDetailsAsync(Guid customerId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Category)
                .Where(o => o.CustomerId == customerId)
                .ToListAsync();
        }

        public async Task<CustomerSpendingStatsDto> GetSpendingStatsAsync(Guid customerId, string period)
        {
            var now = DateTimeHelper.GetVietnamTime();
            var (currentStart, currentEnd, previousStart, previousEnd) = GetPeriodDates(now, period);

            // Get all customer orders with Items, Product, and Category included
            var customerOrders = await GetCustomerOrdersWithDetailsAsync(customerId);

            // Current period orders
            var currentOrders = customerOrders
                .Where(o => o.CreatedAt >= currentStart && o.CreatedAt <= currentEnd)
                .ToList();

            // Previous period orders
            var previousOrders = customerOrders
                .Where(o => o.CreatedAt >= previousStart && o.CreatedAt < previousEnd)
                .ToList();

            // Calculate current period spending
            var currentSpending = currentOrders.Sum(o => o.TotalAmount);
            var previousSpending = previousOrders.Sum(o => o.TotalAmount);
            
            // Calculate spending change percentage
            var spendingChangePercentage = previousSpending > 0 
                ? ((currentSpending - previousSpending) / previousSpending) * 100 
                : (currentSpending > 0 ? 100 : 0);

            // Orders count
            var currentOrdersCount = currentOrders.Count;
            var previousOrdersCount = previousOrders.Count;
            var ordersChangePercentage = previousOrdersCount > 0 
                ? ((decimal)(currentOrdersCount - previousOrdersCount) / previousOrdersCount) * 100 
                : (currentOrdersCount > 0 ? 100 : 0);

            // Total spent (all time)
            var totalSpent = customerOrders.Sum(o => o.TotalAmount);
            var totalSpentChangePercentage = spendingChangePercentage; // Same as current period change

            // Most Rented Category (from returned orders in current period)
            var returnedOrders = currentOrders
                .Where(o => o.Status == OrderStatus.returned)
                .ToList();

            var categoryRentals = new Dictionary<string, int>();
            
            foreach (var order in returnedOrders)
            {
                if (order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        if (item.Product?.Category != null)
                        {
                            var categoryName = item.Product.Category.Name;
                            if (!categoryRentals.ContainsKey(categoryName))
                            {
                                categoryRentals[categoryName] = 0;
                            }
                            categoryRentals[categoryName]++;
                        }
                    }
                }
            }

            var favoriteCategory = "N/A";
            var favoriteCategoryCount = 0;

            if (categoryRentals.Any())
            {
                var topCategory = categoryRentals.OrderByDescending(x => x.Value).First();
                favoriteCategory = topCategory.Key;
                favoriteCategoryCount = topCategory.Value;
            }

            return new CustomerSpendingStatsDto
            {
                ThisMonthSpending = currentSpending,
                OrdersCount = currentOrdersCount,
                TotalSpent = totalSpent,
                FavoriteCategory = favoriteCategory,
                FavoriteCategoryRentalCount = favoriteCategoryCount,
                SpendingChangePercentage = spendingChangePercentage,
                OrdersChangePercentage = ordersChangePercentage,
                TotalSpentChangePercentage = totalSpentChangePercentage
            };
        }

        public async Task<List<SpendingTrendDto>> GetSpendingTrendAsync(Guid customerId, string period)
        {
            var now = DateTimeHelper.GetVietnamTime();
            var (startDate, endDate) = GetCurrentPeriodRange(now, period);

            var customerOrders = await GetCustomerOrdersWithDetailsAsync(customerId);
            var filteredOrders = customerOrders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToList();

            var trendData = new List<SpendingTrendDto>();

            if (period == "week")
            {
                // 7 days
                var startOfWeek = startDate;
                for (int i = 0; i < 7; i++)
                {
                    var date = startOfWeek.AddDays(i);
                    var dayOrders = filteredOrders.Where(o => o.CreatedAt.Date == date.Date);
                    var amount = dayOrders.Sum(o => o.TotalAmount);

                    trendData.Add(new SpendingTrendDto
                    {
                        Date = date.ToString("ddd"), // Mon, Tue, etc.
                        Amount = amount
                    });
                }
            }
            else if (period == "year")
            {
                // 12 months
                var year = startDate.Year;
                for (int month = 1; month <= 12; month++)
                {
                    var monthStart = new DateTime(year, month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    var monthOrders = filteredOrders.Where(o => o.CreatedAt.Month == month && o.CreatedAt.Year == year);
                    var amount = monthOrders.Sum(o => o.TotalAmount);

                    trendData.Add(new SpendingTrendDto
                    {
                        Date = monthStart.ToString("MMM"), // Jan, Feb, etc.
                        Amount = amount
                    });
                }
            }
            else // month
            {
                // Days in current month
                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(startDate.Year, startDate.Month, day);
                    var dayOrders = filteredOrders.Where(o => o.CreatedAt.Date == date.Date);
                    var amount = dayOrders.Sum(o => o.TotalAmount);

                    trendData.Add(new SpendingTrendDto
                    {
                        Date = date.ToString("MMM dd"), // Oct 17, etc.
                        Amount = amount
                    });
                }
            }

            return trendData;
        }

        public async Task<List<OrderStatusBreakdownDto>> GetOrderStatusBreakdownAsync(Guid customerId, string period)
        {
            var now = DateTimeHelper.GetVietnamTime();
            var (startDate, endDate) = GetCurrentPeriodRange(now, period);

            var allCustomerOrders = await GetCustomerOrdersWithDetailsAsync(customerId);
            var customerOrders = allCustomerOrders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToList();

            var totalOrders = customerOrders.Count;
            if (totalOrders == 0)
            {
                return new List<OrderStatusBreakdownDto>();
            }

            var statusGroups = customerOrders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusBreakdownDto
                {
                    Status = FormatStatusName(g.Key),
                    Count = g.Count(),
                    Percentage = Math.Round((decimal)g.Count() / totalOrders * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return statusGroups;
        }

        public async Task<List<SpendingByCategoryDto>> GetSpendingByCategoryAsync(Guid customerId, string period)
        {
            var now = DateTimeHelper.GetVietnamTime();
            var (startDate, endDate) = GetCurrentPeriodRange(now, period);

            var allCustomerOrders = await GetCustomerOrdersWithDetailsAsync(customerId);
            
            // Only get returned orders (completed rentals)
            var returnedOrders = allCustomerOrders
                .Where(o => o.Status == OrderStatus.returned 
                         && o.CreatedAt >= startDate 
                         && o.CreatedAt <= endDate)
                .ToList();

            // Group by category and calculate spending
            var categorySpending = new Dictionary<string, (decimal totalSpending, int orderCount)>();

            foreach (var order in returnedOrders)
            {
                if (order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        if (item.Product?.Category != null)
                        {
                            var categoryName = item.Product.Category.Name;
                            
                            if (!categorySpending.ContainsKey(categoryName))
                            {
                                categorySpending[categoryName] = (0, 0);
                            }

                            // Add item's portion of total order amount
                            // For simplicity, we'll distribute the order amount evenly across items
                            var itemCount = order.Items.Count;
                            var itemAmount = order.TotalAmount / itemCount;
                            
                            categorySpending[categoryName] = (
                                categorySpending[categoryName].totalSpending + itemAmount,
                                categorySpending[categoryName].orderCount + 1
                            );
                        }
                    }
                }
            }

            var result = categorySpending
                .Select(kvp => new SpendingByCategoryDto
                {
                    CategoryName = kvp.Key,
                    TotalSpending = kvp.Value.totalSpending,
                    OrderCount = kvp.Value.orderCount
                })
                .OrderByDescending(x => x.TotalSpending)
                .ToList();

            return result;
        }

        private string FormatStatusName(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.pending => "Pending",
                OrderStatus.approved => "Approved",
                OrderStatus.in_transit => "In Transit",
                OrderStatus.in_use => "In Use",
                OrderStatus.returning => "Returning",
                OrderStatus.returned => "Returned",
                OrderStatus.cancelled => "Cancelled",
                OrderStatus.returned_with_issue => "Returned with Issue",
                _ => status.ToString()
            };
        }

        private (DateTime currentStart, DateTime currentEnd, DateTime previousStart, DateTime previousEnd) GetPeriodDates(DateTime now, string period)
        {
            DateTime currentStart, currentEnd, previousStart, previousEnd;

            switch (period.ToLower())
            {
                case "week":
                    // Current week (Sunday to Saturday)
                    var dayOfWeek = (int)now.DayOfWeek;
                    currentStart = now.Date.AddDays(-dayOfWeek);
                    currentEnd = currentStart.AddDays(7).AddSeconds(-1);
                    
                    // Previous week
                    previousStart = currentStart.AddDays(-7);
                    previousEnd = currentStart.AddSeconds(-1);
                    break;

                case "year":
                    // Current year
                    currentStart = new DateTime(now.Year, 1, 1);
                    currentEnd = new DateTime(now.Year, 12, 31, 23, 59, 59);
                    
                    // Previous year
                    previousStart = new DateTime(now.Year - 1, 1, 1);
                    previousEnd = new DateTime(now.Year - 1, 12, 31, 23, 59, 59);
                    break;

                default: // month
                    // Current month
                    currentStart = new DateTime(now.Year, now.Month, 1);
                    currentEnd = currentStart.AddMonths(1).AddSeconds(-1);
                    
                    // Previous month
                    previousStart = currentStart.AddMonths(-1);
                    previousEnd = currentStart.AddSeconds(-1);
                    break;
            }

            return (currentStart, currentEnd, previousStart, previousEnd);
        }

        private (DateTime startDate, DateTime endDate) GetCurrentPeriodRange(DateTime now, string period)
        {
            DateTime startDate, endDate;

            switch (period.ToLower())
            {
                case "week":
                    var dayOfWeek = (int)now.DayOfWeek;
                    startDate = now.Date.AddDays(-dayOfWeek);
                    endDate = startDate.AddDays(7).AddSeconds(-1);
                    break;

                case "year":
                    startDate = new DateTime(now.Year, 1, 1);
                    endDate = new DateTime(now.Year, 12, 31, 23, 59, 59);
                    break;

                default: // month
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.AddMonths(1).AddSeconds(-1);
                    break;
            }

            return (startDate, endDate);
        }
    }
}

