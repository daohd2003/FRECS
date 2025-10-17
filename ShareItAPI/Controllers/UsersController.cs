using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.UserServices;
using Services.Utilities;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(new ApiResponse<object>("Success", users));
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<User>("Success", user));
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            await _userService.AddAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new ApiResponse<User>("User created successfully", user));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> Update(Guid id, User user)
        {
            var validation = GuidUtilities.ValidateGuid(id.ToString(), user.Id);
            if (!validation.IsValid)
            {
                return BadRequest(new ApiResponse<string>(validation.ErrorMessage, null));
            }

            // Mã hóa mật khẩu (giả sử user.PasswordHash chứa mật khẩu plain text)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            var result = await _userService.UpdateAsync(user);
            if (!result)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<string>("User updated successfully", null));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _userService.DeleteAsync(id);
            if (!result)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<string>("User deleted successfully", null));
        }

        [HttpGet("admins")]
        [Authorize(Roles = "admin")] // Chỉ admin mới được lấy danh sách các admin khác
        public async Task<IActionResult> GetAllAdmins()
        {
            var admins = await _userService.GetAllAdminsAsync();
            return Ok(new ApiResponse<IEnumerable<AdminViewModel>>("Fetched admins successfully.", admins));
        }

        [HttpGet("search-by-email")]
        [Authorize(Roles = "provider,customer")]
        public async Task<IActionResult> SearchByEmail()
        {
            var users = await _userService.GetAllAsync();
            return Ok(new ApiResponse<object>("Success", users));
        }

        [HttpGet("customers-and-providers")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetCustomersAndProviders()
        {
            var users = await _userService.GetCustomersAndProvidersAsync();
            return Ok(new ApiResponse<object>("Success", users));
        }

        [HttpPost("{id}/block")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> BlockUser(Guid id)
        {
            var blocked = await _userService.BlockUserAsync(id);
            if (!blocked)
            {
                return NotFound(new ApiResponse<string>("User not found", null));
            }

            return Ok(new ApiResponse<string>("User blocked (set inactive) successfully", null));
        }

        [HttpPost("{id}/unblock")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> UnblockUser(Guid id)
        {
            var unblocked = await _userService.UnblockUserAsync(id);
            if (!unblocked)
            {
                return NotFound(new ApiResponse<string>("User not found", null));
            }

            return Ok(new ApiResponse<string>("User unblocked (set active) successfully", null));
        }

        [HttpGet("with-order-stats")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetAllWithOrderStats()
        {
            var users = await _userService.GetAllWithOrdersAsync();
            
            // Calculate order statistics for each user
            var usersWithStats = users.Select(user => 
            {
                // Get relevant orders based on role
                var ordersAsCustomer = user.OrdersAsCustomer ?? new List<Order>();
                var ordersAsProvider = user.OrdersAsProvider ?? new List<Order>();
                
                // For customers: count their orders as customer
                // For providers: count their orders as provider
                var relevantOrders = user.Role.ToString() == "provider" 
                    ? ordersAsProvider 
                    : ordersAsCustomer;

                // Count orders by status
                var ordersByStatus = new
                {
                    Pending = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.pending),
                    Approved = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.approved),
                    InTransit = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.in_transit),
                    InUse = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.in_use),
                    Returning = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.returning),
                    Returned = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.returned),
                    Cancelled = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.cancelled),
                    ReturnedWithIssue = relevantOrders.Count(o => o.Status == BusinessObject.Enums.OrderStatus.returned_with_issue)
                };

                // Calculate earnings/spending for returned orders by PRODUCT TYPE (not order type)
                var returnedOrders = relevantOrders
                    .Where(o => o.Status == BusinessObject.Enums.OrderStatus.returned || 
                                o.Status == BusinessObject.Enums.OrderStatus.returned_with_issue)
                    .ToList();

                // Get all items from returned orders
                var allReturnedItems = returnedOrders
                    .Where(o => o.Items != null)
                    .SelectMany(o => o.Items)
                    .ToList();

                // Count rental and purchase products (not orders)
                var rentalProducts = allReturnedItems
                    .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental)
                    .ToList();
                
                var purchaseProducts = allReturnedItems
                    .Where(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                    .ToList();

                // Calculate rental earnings: Sum of (DailyRate * RentalDays * Quantity) for rental items
                var rentalEarnings = rentalProducts.Sum(item => 
                    item.DailyRate * (item.RentalDays ?? 0) * item.Quantity);

                // Calculate purchase earnings: Sum of (DailyRate * Quantity) for purchase items
                // Note: For purchase items, DailyRate is the purchase price per item
                var purchaseEarnings = purchaseProducts.Sum(item => 
                    item.DailyRate * item.Quantity);

                return new
                {
                    user.Id,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt,
                    user.LastLogin,
                    user.Profile,
                    
                    // Total count
                    TotalOrders = relevantOrders.Count(),
                    
                    // Orders by status
                    OrdersByStatus = ordersByStatus,
                    
                    // Returned orders breakdown by PRODUCT TYPE
                    ReturnedOrdersBreakdown = new
                    {
                        RentalProductsCount = rentalProducts.Count,
                        RentalTotalEarnings = rentalEarnings,
                        PurchaseProductsCount = purchaseProducts.Count,
                        PurchaseTotalEarnings = purchaseEarnings,
                        TotalEarnings = rentalEarnings + purchaseEarnings,
                        // Keep old names for backward compatibility
                        RentalOrdersCount = rentalProducts.Count,
                        PurchaseOrdersCount = purchaseProducts.Count
                    }
                };
            }).ToList();
            
            // For staff, only return customers and providers
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole?.ToLower() == "staff")
            {
                usersWithStats = usersWithStats
                    .Where(u => u.Role.ToString() == "customer" || u.Role.ToString() == "provider")
                    .ToList();
            }
            
            return Ok(new ApiResponse<object>("Success", usersWithStats));
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> GetStatistics()
        {
            var users = await _userService.GetAllAsync();
            
            // For staff, only count customers and providers
            var usersToCount = users;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userRole?.ToLower() == "staff")
            {
                usersToCount = users.Where(u => u.Role.ToString() == "customer" || u.Role.ToString() == "provider");
            }

            var totalUsers = usersToCount.Count();
            var activeUsers = usersToCount.Count(u => u.IsActive == true);
            var inactiveUsers = usersToCount.Count(u => u.IsActive == false);

            // Calculate new users this month (current)
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            var newThisMonth = usersToCount.Count(u => u.CreatedAt >= startOfMonth && u.CreatedAt <= endOfMonth);

            // Calculate new users last month (previous)
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddSeconds(-1);
            var newLastMonth = usersToCount.Count(u => u.CreatedAt >= startOfLastMonth && u.CreatedAt <= endOfLastMonth);

            // Calculate trend percentage
            double trendPercentage = 0;
            string trendDirection = "stable";
            
            if (newLastMonth > 0)
            {
                // Calculate percentage change: ((current - previous) / previous) * 100
                trendPercentage = ((double)(newThisMonth - newLastMonth) / newLastMonth) * 100;
                
                if (trendPercentage > 0.5) // Small threshold to avoid floating point issues
                    trendDirection = "up";
                else if (trendPercentage < -0.5)
                    trendDirection = "down";
                else
                    trendDirection = "stable";
            }
            else if (newThisMonth > 0)
            {
                // If no users last month but have this month = 100% growth
                trendPercentage = 100;
                trendDirection = "up";
            }

            // Calculate additional insights
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var currentDay = now.Day;
            var monthProgress = (double)currentDay / daysInMonth * 100;
            
            // Calculate average per day
            var avgPerDayThisMonth = currentDay > 0 ? (double)newThisMonth / currentDay : 0;
            var daysInLastMonth = DateTime.DaysInMonth(startOfLastMonth.Year, startOfLastMonth.Month);
            var avgPerDayLastMonth = daysInLastMonth > 0 ? (double)newLastMonth / daysInLastMonth : 0;

            // Projected end of month (based on current average)
            var projectedEndOfMonth = avgPerDayThisMonth > 0 ? (int)Math.Round(avgPerDayThisMonth * daysInMonth) : newThisMonth;

            // Build response
            var statistics = new
            {
                // Basic counts
                totalUsers,
                activeUsers,
                inactiveUsers,
                
                // This month data
                newThisMonth,
                currentMonthName = now.ToString("MMMM"),
                currentMonthYear = now.Year,
                
                // Last month data
                newLastMonth,
                lastMonthName = startOfLastMonth.ToString("MMMM"),
                lastMonthYear = startOfLastMonth.Year,
                
                // Trend analysis
                trendPercentage = Math.Round(trendPercentage, 1),
                trendDirection,
                absoluteChange = newThisMonth - newLastMonth,
                
                // Month progress
                monthProgress = Math.Round(monthProgress, 1),
                currentDay,
                daysInMonth,
                daysRemaining = daysInMonth - currentDay,
                
                // Averages and projections
                avgPerDayThisMonth = Math.Round(avgPerDayThisMonth, 2),
                avgPerDayLastMonth = Math.Round(avgPerDayLastMonth, 2),
                projectedEndOfMonth,
                projectedGrowth = projectedEndOfMonth - newLastMonth,
                
                // Timestamps
                calculatedAt = now,
                monthStart = startOfMonth,
                monthEnd = endOfMonth,
                lastMonthStart = startOfLastMonth,
                lastMonthEnd = endOfLastMonth
            };

            return Ok(new ApiResponse<object>("Statistics retrieved successfully", statistics));
        }
    }
}
