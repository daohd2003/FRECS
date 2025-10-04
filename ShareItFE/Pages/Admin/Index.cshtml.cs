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

        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalDiscountCodes { get; set; }
        public List<ActivityItem> RecentActivities { get; set; } = new List<ActivityItem>();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                // Fetch statistics (you'll need to create these API endpoints or use existing ones)
                try
                {
                    var usersResponse = await client.GetAsync($"{apiBaseUrl}/api/users/count");
                    if (usersResponse.IsSuccessStatusCode)
                    {
                        var content = await usersResponse.Content.ReadAsStringAsync();
                        TotalUsers = JsonSerializer.Deserialize<int>(content);
                    }
                }
                catch { TotalUsers = 0; }

                try
                {
                    var productsResponse = await client.GetAsync($"{apiBaseUrl}/api/products/count");
                    if (productsResponse.IsSuccessStatusCode)
                    {
                        var content = await productsResponse.Content.ReadAsStringAsync();
                        TotalProducts = JsonSerializer.Deserialize<int>(content);
                    }
                }
                catch { TotalProducts = 0; }

                try
                {
                    var ordersResponse = await client.GetAsync($"{apiBaseUrl}/api/orders/count");
                    if (ordersResponse.IsSuccessStatusCode)
                    {
                        var content = await ordersResponse.Content.ReadAsStringAsync();
                        TotalOrders = JsonSerializer.Deserialize<int>(content);
                    }
                }
                catch { TotalOrders = 0; }

                try
                {
                    var discountResponse = await client.GetAsync($"{apiBaseUrl}/api/DiscountCode");
                    if (discountResponse.IsSuccessStatusCode)
                    {
                        var content = await discountResponse.Content.ReadAsStringAsync();
                        var response = JsonSerializer.Deserialize<BusinessObject.DTOs.ApiResponses.ApiResponse<IEnumerable<object>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        TotalDiscountCodes = response?.Data?.Count() ?? 0;
                    }
                }
                catch { TotalDiscountCodes = 0; }

                // Mock recent activities (you can fetch real data from API)
                RecentActivities = new List<ActivityItem>
                {
                    new ActivityItem { Type = "User", Description = "New user registered", TimeAgo = "5 minutes ago" },
                    new ActivityItem { Type = "Order", Description = "New order received", TimeAgo = "15 minutes ago" },
                    new ActivityItem { Type = "Product", Description = "Product approved", TimeAgo = "1 hour ago" },
                    new ActivityItem { Type = "User", Description = "User updated profile", TimeAgo = "2 hours ago" },
                };

                return Page();
            }
            catch (Exception ex)
            {
                // Log error
                return RedirectToPage("/Error");
            }
        }
    }

    public class ActivityItem
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
    }
}

