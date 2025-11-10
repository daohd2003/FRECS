using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DashboardStatsDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text.Json;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class IndexModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public AdminDashboardDto DashboardData { get; set; } = new AdminDashboardDto();
        public string ErrorMessage { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Preset { get; set; } = "Last 30 Days";

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Set default date range if not provided
                if (!StartDate.HasValue || !EndDate.HasValue)
                {
                    EndDate = DateTime.UtcNow;
                    StartDate = EndDate.Value.AddDays(-30);
                    Preset = "Last 30 Days";
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                // Fetch dashboard data
                var response = await client.GetAsync($"{apiBaseUrl}/dashboard/admin?startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<AdminDashboardDto>>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    if (apiResponse?.Data != null)
                    {
                        DashboardData = apiResponse.Data;
                    }
                    else
                    {
                        ErrorMessage = "Dashboard data is null. API returned empty data.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to load dashboard data. Status: {response.StatusCode}. Error: {errorContent}";
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading dashboard: {ex.Message}. Stack: {ex.StackTrace}";
                // Log to console for debugging
                Console.WriteLine($"Dashboard Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Page();
            }
        }

        public string GetTimeAgo(DateTime timestamp)
        {
            var timeSpan = DateTime.UtcNow - timestamp;
            
            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} days ago";
            
            return timestamp.ToString("MMM dd, yyyy");
        }

        public string GetChangeColor(decimal change)
        {
            return change >= 0 ? "text-green-600" : "text-red-600";
        }

        public string GetChangeIcon(decimal change)
        {
            return change >= 0 ? "trending-up" : "trending-down";
        }
    }
}

