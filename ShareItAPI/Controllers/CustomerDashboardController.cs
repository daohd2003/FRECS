using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CustomerDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CustomerDashboardServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/customer/dashboard")]
    [Authorize(Roles = "customer")]
    public class CustomerDashboardController : ControllerBase
    {
        private readonly ICustomerDashboardService _dashboardService;

        public CustomerDashboardController(ICustomerDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Get customer spending statistics by period
        /// </summary>
        /// <param name="period">week, month, or year</param>
        [HttpGet("spending-stats")]
        public async Task<IActionResult> GetSpendingStats([FromQuery] string period = "month")
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var stats = await _dashboardService.GetSpendingStatsAsync(customerId, period);
            
            return Ok(new ApiResponse<CustomerSpendingStatsDto>("Spending statistics retrieved", stats));
        }

        /// <summary>
        /// Get spending trend data for charts
        /// </summary>
        /// <param name="period">week, month, or year</param>
        [HttpGet("spending-trend")]
        public async Task<IActionResult> GetSpendingTrend([FromQuery] string period = "month")
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var trend = await _dashboardService.GetSpendingTrendAsync(customerId, period);
            
            return Ok(new ApiResponse<List<SpendingTrendDto>>("Spending trend retrieved", trend));
        }

        /// <summary>
        /// Get order status breakdown for pie chart
        /// </summary>
        /// <param name="period">week, month, or year</param>
        [HttpGet("order-status-breakdown")]
        public async Task<IActionResult> GetOrderStatusBreakdown([FromQuery] string period = "month")
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var breakdown = await _dashboardService.GetOrderStatusBreakdownAsync(customerId, period);
            
            return Ok(new ApiResponse<List<OrderStatusBreakdownDto>>("Order status breakdown retrieved", breakdown));
        }

        /// <summary>
        /// Get spending by category for bar chart
        /// </summary>
        /// <param name="period">week, month, or year</param>
        [HttpGet("spending-by-category")]
        public async Task<IActionResult> GetSpendingByCategory([FromQuery] string period = "month")
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new ApiResponse<object>("User not authenticated", null));
            }

            var customerId = Guid.Parse(userIdClaim);
            var categorySpending = await _dashboardService.GetSpendingByCategoryAsync(customerId, period);
            
            return Ok(new ApiResponse<List<SpendingByCategoryDto>>("Spending by category retrieved", categorySpending));
        }
    }
}

