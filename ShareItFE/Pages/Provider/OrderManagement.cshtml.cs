using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace ShareItFE.Pages.Provider
{
    [Authorize] // Require authentication but check role manually in methods
    public class OrderManagementModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderManagementModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        // Properties
        public List<OrderListDto> Orders { get; set; } = new List<OrderListDto>();
        public List<OrderListDto> AllOrders { get; set; } = new List<OrderListDto>();
        
        // Stats - Provider focused
        public int ToShipCount { get; set; } // Approved - Need to ship
        public int InTransitCount { get; set; } // In Transit - Currently delivering
        public int InUseCount { get; set; } // In Use - Active rentals (revenue)
        public int ToConfirmReturnCount { get; set; } // Returning - Need to confirm return
        
        // Additional stats for internal use
        public int PendingCount { get; set; } // Customer hasn't paid (not provider's concern)
        public int CompletedCount { get; set; } // Returned
        public int CancelledCount { get; set; }

        // Pagination properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        public int TotalOrdersCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalOrdersCount / PageSize);

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        // Status filter options
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "All", Text = "All Status" },
            new SelectListItem { Value = "pending", Text = "Pending" },
            new SelectListItem { Value = "approved", Text = "Approved" },
            new SelectListItem { Value = "in_transit", Text = "In Transit" },
            new SelectListItem { Value = "in_use", Text = "In Use" },
            new SelectListItem { Value = "returning", Text = "Returning" },
            new SelectListItem { Value = "returned", Text = "Returned" },
            new SelectListItem { Value = "returned_with_issue", Text = "Issue Reported" },
            new SelectListItem { Value = "cancelled", Text = "Cancelled" }
        };

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            // Verify user has provider role
            if (!User.IsInRole("provider"))
            {
                TempData["ErrorMessage"] = "Access Denied. You do not have permission to access this page.";
                return RedirectToPage("/Index");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                await LoadProviderOrdersAsync(client, userId);
                CalculateStats();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading orders: {ex.Message}";
            }

            return Page();
        }

        private async Task LoadProviderOrdersAsync(HttpClient client, string providerId)
        {
            // Call API to get orders for this provider
            var requestUri = $"api/orders/provider/{providerId}/list-display";
            
            var response = await client.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                
                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderListDto>>>(jsonOptions);
                if (apiResponse?.Data != null)
                {
                    var ordersData = apiResponse.Data;
                    AllOrders = ordersData;
                    
                    // Apply filters
                    var filteredOrders = ordersData.AsEnumerable();

                    if (!string.IsNullOrEmpty(SearchQuery))
                    {
                        filteredOrders = filteredOrders.Where(o =>
                            o.OrderCode.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                            o.CustomerName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                            o.CustomerEmail.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                        );
                    }

                    if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All")
                    {
                        if (Enum.TryParse<OrderStatus>(StatusFilter, true, out var status))
                        {
                            filteredOrders = filteredOrders.Where(o => o.Status == status);
                        }
                    }

                    // Get total count for pagination
                    TotalOrdersCount = filteredOrders.Count();

                    // Apply pagination
                    Orders = filteredOrders
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .ToList();
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = $"Error loading orders: {errorContent}";
            }
        }

        private void CalculateStats()
        {
            // Get all orders (before filter) to calculate stats - Provider workflow focused
            
            // To Ship: Orders that need to be shipped (customer paid, provider needs to deliver)
            ToShipCount = AllOrders.Count(o => o.Status == OrderStatus.approved);
            
            // In Transit: Currently being delivered
            InTransitCount = AllOrders.Count(o => o.Status == OrderStatus.in_transit);
            
            // In Use: Active rentals (generating revenue)
            InUseCount = AllOrders.Count(o => o.Status == OrderStatus.in_use);
            
            // To Confirm Return: Items being returned, need provider confirmation
            ToConfirmReturnCount = AllOrders.Count(o => o.Status == OrderStatus.returning);
            
            // Additional stats (not displayed in main cards)
            PendingCount = AllOrders.Count(o => o.Status == OrderStatus.pending);
            CompletedCount = AllOrders.Count(o => 
                o.Status == OrderStatus.returned || 
                o.Status == OrderStatus.returned_with_issue);
            CancelledCount = AllOrders.Count(o => o.Status == OrderStatus.cancelled);
        }

        public async Task<IActionResult> OnPostShipOrderAsync(string orderId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            // Verify user has provider role
            if (!User.IsInRole("provider"))
            {
                TempData["ErrorMessage"] = "Access Denied.";
                return RedirectToPage("/Index");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                
                // Use the existing endpoint to mark as shipping
                var response = await client.PutAsync($"api/orders/{orderId}/mark-shipping", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Order shipped successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Error shipping order: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error shipping order: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostConfirmReturnAsync(string orderId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            // Verify user has provider role
            if (!User.IsInRole("provider"))
            {
                TempData["ErrorMessage"] = "Access Denied.";
                return RedirectToPage("/Index");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                
                // Use the existing endpoint to mark as returned
                var response = await client.PutAsync($"api/orders/{orderId}/mark-returned", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Order marked as returned successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Error confirming return: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error confirming return: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(authToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }
            return client;
        }

        public string GetStatusBadgeClass(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.pending => "bg-yellow-100 text-yellow-700",
                OrderStatus.approved => "bg-purple-100 text-purple-700",
                OrderStatus.in_transit => "bg-blue-100 text-blue-700",
                OrderStatus.in_use => "bg-green-100 text-green-700",
                OrderStatus.returning => "bg-orange-100 text-orange-700",
                OrderStatus.returned => "bg-gray-100 text-gray-700",
                OrderStatus.cancelled => "bg-red-100 text-red-700",
                OrderStatus.returned_with_issue => "bg-orange-100 text-orange-800",
                _ => "bg-gray-100 text-gray-700"
            };
        }

        public string GetStatusIcon(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.pending => "clock",
                OrderStatus.approved => "check-circle",
                OrderStatus.in_transit => "truck",
                OrderStatus.in_use => "box",
                OrderStatus.returning => "rotate-ccw",
                OrderStatus.returned => "check-circle",
                OrderStatus.cancelled => "x-circle",
                OrderStatus.returned_with_issue => "alert-triangle",
                _ => "circle"
            };
        }

        public string GetStatusDisplayText(OrderStatus status)
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
                OrderStatus.returned_with_issue => "Issue Reported",
                _ => status.ToString()
            };
        }
    }
}

