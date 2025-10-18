using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CustomerDashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Customer
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public DashboardModel(
            ILogger<DashboardModel> logger,
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public CustomerSpendingStatsDto Stats { get; set; } = new();
        public List<SpendingTrendDto> SpendingTrend { get; set; } = new();
        public List<OrderStatusBreakdownDto> OrderStatusBreakdown { get; set; } = new();
        public List<SpendingByCategoryDto> SpendingByCategory { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "month"; // week, month, year

        public string PeriodLabel => Period switch
        {
            "week" => "This Week's Spending",
            "year" => "This Year's Spending",
            _ => "This Month's Spending"
        };

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            try
            {
                await LoadDataFromApi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                // Fallback to mock data if API fails
                LoadMockData();
            }

            return Page();
        }

        private async Task LoadDataFromApi()
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1. Get spending stats
            var statsResponse = await client.GetAsync($"api/customer/dashboard/spending-stats?period={Period}");
            if (statsResponse.IsSuccessStatusCode)
            {
                var statsContent = await statsResponse.Content.ReadAsStringAsync();
                var statsApiResponse = JsonSerializer.Deserialize<ApiResponse<CustomerSpendingStatsDto>>(statsContent, options);
                Stats = statsApiResponse?.Data ?? new CustomerSpendingStatsDto();
            }

            // 2. Get spending trend
            var trendResponse = await client.GetAsync($"api/customer/dashboard/spending-trend?period={Period}");
            if (trendResponse.IsSuccessStatusCode)
            {
                var trendContent = await trendResponse.Content.ReadAsStringAsync();
                var trendApiResponse = JsonSerializer.Deserialize<ApiResponse<List<SpendingTrendDto>>>(trendContent, options);
                SpendingTrend = trendApiResponse?.Data ?? new List<SpendingTrendDto>();
            }

            // 3. Get order status breakdown
            var breakdownResponse = await client.GetAsync($"api/customer/dashboard/order-status-breakdown?period={Period}");
            if (breakdownResponse.IsSuccessStatusCode)
            {
                var breakdownContent = await breakdownResponse.Content.ReadAsStringAsync();
                var breakdownApiResponse = JsonSerializer.Deserialize<ApiResponse<List<OrderStatusBreakdownDto>>>(breakdownContent, options);
                OrderStatusBreakdown = breakdownApiResponse?.Data ?? new List<OrderStatusBreakdownDto>();
            }

            // 4. Get spending by category
            var categoryResponse = await client.GetAsync($"api/customer/dashboard/spending-by-category?period={Period}");
            if (categoryResponse.IsSuccessStatusCode)
            {
                var categoryContent = await categoryResponse.Content.ReadAsStringAsync();
                var categoryApiResponse = JsonSerializer.Deserialize<ApiResponse<List<SpendingByCategoryDto>>>(categoryContent, options);
                SpendingByCategory = categoryApiResponse?.Data ?? new List<SpendingByCategoryDto>();
            }
        }

        private void LoadMockData()
        {
            // Mock statistics based on period
            // Note: Most Rented Category is calculated from orders with status "returned" only
            // This represents completed rental cycles where the customer successfully returned the items
            if (Period == "week")
            {
                Stats = new CustomerSpendingStatsDto
                {
                    ThisMonthSpending = 125.00m, // This week's spending
                    OrdersCount = 2,
                    TotalSpent = 125.00m,
                    FavoriteCategory = "Cocktail Dresses",
                    FavoriteCategoryRentalCount = 2,
                    SpendingChangePercentage = 15.5m,
                    OrdersChangePercentage = 100.0m,
                    TotalSpentChangePercentage = 15.5m
                };

                // Weekly trend (7 days)
                SpendingTrend = new List<SpendingTrendDto>();
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                
                for (int i = 0; i < 7; i++)
                {
                    var date = startOfWeek.AddDays(i);
                    decimal amount = 0;
                    
                    if (i == 2) amount = 75.00m; // Tuesday
                    if (i == 5) amount = 50.00m; // Friday

                    SpendingTrend.Add(new SpendingTrendDto
                    {
                        Date = date.ToString("ddd"),
                        Amount = amount
                    });
                }

                // Weekly order status breakdown
                OrderStatusBreakdown = new List<OrderStatusBreakdownDto>
                {
                    new OrderStatusBreakdownDto { Status = "In Use", Count = 2, Percentage = 100 }
                };
            }
            else if (Period == "year")
            {
                Stats = new CustomerSpendingStatsDto
                {
                    ThisMonthSpending = 3850.00m, // This year's spending
                    OrdersCount = 15,
                    TotalSpent = 3850.00m,
                    FavoriteCategory = "Evening Dresses",
                    FavoriteCategoryRentalCount = 6,
                    SpendingChangePercentage = 42.3m,
                    OrdersChangePercentage = 50.0m,
                    TotalSpentChangePercentage = 42.3m
                };

                // Yearly trend (12 months)
                SpendingTrend = new List<SpendingTrendDto>();
                var currentYear = DateTime.Today.Year;
                
                for (int i = 1; i <= 12; i++)
                {
                    var date = new DateTime(currentYear, i, 1);
                    decimal amount = 0;
                    
                    // Simulate varying spending throughout the year
                    if (i == 2) amount = 450.00m;  // February
                    if (i == 5) amount = 380.00m;  // May
                    if (i == 7) amount = 520.00m;  // July
                    if (i == 10) amount = 410.00m; // October
                    if (i == 12) amount = 680.00m; // December

                    SpendingTrend.Add(new SpendingTrendDto
                    {
                        Date = date.ToString("MMM"),
                        Amount = amount
                    });
                }

                // Yearly order status breakdown
                OrderStatusBreakdown = new List<OrderStatusBreakdownDto>
                {
                    new OrderStatusBreakdownDto { Status = "Returned", Count = 10, Percentage = 66.7m },
                    new OrderStatusBreakdownDto { Status = "In Use", Count = 3, Percentage = 20.0m },
                    new OrderStatusBreakdownDto { Status = "In Transit", Count = 2, Percentage = 13.3m }
                };
            }
            else // month
            {
                Stats = new CustomerSpendingStatsDto
                {
                    ThisMonthSpending = 285.00m, // This month's spending
                    OrdersCount = 4,
                    TotalSpent = 1250.00m,
                    FavoriteCategory = "Evening Dresses",
                    FavoriteCategoryRentalCount = 3,
                    SpendingChangePercentage = 12.8m,
                    OrdersChangePercentage = 33.3m,
                    TotalSpentChangePercentage = 27.6m
                };

                // Monthly trend (current month days)
                SpendingTrend = new List<SpendingTrendDto>();
                var today = DateTime.Today;
                var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                var startDate = new DateTime(today.Year, today.Month, 1);
                
                for (int i = 0; i < daysInMonth; i++)
                {
                    var date = startDate.AddDays(i);
                    decimal amount = 0;
                    
                    // Add spending on specific days
                    if (i == 5) amount = 85.00m;
                    if (i == 14) amount = 125.00m;
                    if (i == 23) amount = 75.00m;

                    SpendingTrend.Add(new SpendingTrendDto
                    {
                        Date = date.ToString("MMM dd"),
                        Amount = amount
                    });
                }

                // Monthly order status breakdown
                OrderStatusBreakdown = new List<OrderStatusBreakdownDto>
                {
                    new OrderStatusBreakdownDto { Status = "Returned", Count = 2, Percentage = 50 },
                    new OrderStatusBreakdownDto { Status = "In Use", Count = 1, Percentage = 25 },
                    new OrderStatusBreakdownDto { Status = "Returning", Count = 1, Percentage = 25 }
                };
            }
        }

        public async Task<IActionResult> OnPostExportReportAsync()
        {
            // TODO: Implement report export functionality
            TempData["SuccessMessage"] = "Report export feature coming soon!";
            return RedirectToPage();
        }
    }
}

