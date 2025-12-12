using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DashboardStatsDto;
using BusinessObject.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DashboardServices;
using System;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Roles = "admin,staff")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Test endpoint to verify dashboard service is working
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult TestDashboard()
        {
            try
            {
                return Ok(new { 
                    message = "Dashboard controller is working",
                    serviceInjected = _dashboardService != null,
                    timestamp = DateTimeHelper.GetVietnamTime()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get admin dashboard data with date range filter
        /// </summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminDashboard([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var vietnamNow = DateTimeHelper.GetVietnamTime();
                var filter = new DashboardFilterDto
                {
                    StartDate = startDate ?? vietnamNow.AddDays(-30),
                    EndDate = endDate ?? vietnamNow,
                    Preset = "Custom"
                };

                // Set preset based on date range
                var daysDiff = (filter.EndDate - filter.StartDate).Days;
                if (filter.EndDate.Date == vietnamNow.Date && filter.StartDate.Date == vietnamNow.Date)
                    filter.Preset = "Today";
                else if (daysDiff == 7)
                    filter.Preset = "Last 7 Days";
                else if (daysDiff == 30)
                    filter.Preset = "Last 30 Days";

                var data = await _dashboardService.GetAdminDashboardDataAsync(filter);
                
                return Ok(new ApiResponse<AdminDashboardDto>("Dashboard data retrieved successfully", data));
            }
            catch (Exception ex)
            {
                var errorDetails = new
                {
                    Message = ex.Message,
                    InnerException = ex.InnerException?.Message,
                    StackTrace = ex.StackTrace
                };
                
                return StatusCode(500, new ApiResponse<object>($"Error retrieving dashboard data: {ex.Message}. Inner: {ex.InnerException?.Message}", errorDetails));
            }
        }

        /// <summary>
        /// Get detail items for modal (products, orders, etc.) with date range filter
        /// </summary>
        [HttpGet("details/{type}")]
        public async Task<IActionResult> GetDetailItems(string type, [FromQuery] string? filter, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? search = null)
        {
            try
            {
                var vietnamNow = DateTimeHelper.GetVietnamTime();
                var start = startDate ?? vietnamNow.AddDays(-30);
                var end = endDate ?? vietnamNow;
                
                object data = type.ToLower() switch
                {
                    "products" => await _dashboardService.GetProductDetailsAsync(filter, start, end, search),
                    "orders" => await _dashboardService.GetOrderDetailsAsync(filter, start, end, search),
                    "reports" => await _dashboardService.GetReportDetailsAsync(filter, start, end, search),
                    "violations" => await _dashboardService.GetViolationDetailsAsync(filter, start, end, search),
                    "users" => await _dashboardService.GetUserDetailsAsync(filter, start, end, search),
                    _ => throw new ArgumentException("Invalid type")
                };
                
                return Ok(new ApiResponse<object>("Details retrieved successfully", data));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error retrieving details: {ex.Message}", null));
            }
        }
    }
}

