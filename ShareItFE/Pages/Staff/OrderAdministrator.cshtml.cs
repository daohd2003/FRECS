using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Staff
{
    [Authorize(Roles = "admin,staff")]
    public class OrderAdministratorModel : PageModel
    {
        private readonly ILogger<OrderAdministratorModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;

        public OrderAdministratorModel(
            ILogger<OrderAdministratorModel> logger,
            AuthenticatedHttpClientHelper clientHelper, 
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
        }

        public List<AdminOrderListDto> Orders { get; set; } = new List<AdminOrderListDto>();
        public int TotalOrderCount { get; set; }
        public int TotalRentalOrders { get; set; }
        public int TotalPurchaseOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalRentalRevenue { get; set; }
        public decimal TotalPurchaseRevenue { get; set; }
        public string ErrorMessage { get; set; }
        public string SearchOrder { get; set; }
        public string SelectedTab { get; set; } = "all";
        public string CurrentUserRole { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }

        public async Task<IActionResult> OnGetAsync(string? searchOrder, string? statusFilter, string? tab, int page = 1)
        {
            try
            {
                // Get current user info - EXACTLY like ProductAdministration
                CurrentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                AccessToken = HttpContext.Request.Cookies["AccessToken"] ?? string.Empty;

                // Get API URL dynamically based on environment
                ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                // Set search parameters
                SearchOrder = searchOrder ?? string.Empty;
                SelectedTab = tab ?? "all";
                string filterStatus = statusFilter ?? "all";
                CurrentPage = page;

                // Load orders from API
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("api/orders/admin/all-orders");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<AdminOrderListDto>>>(content, _jsonOptions);
                    
                    if (apiResponse?.Data != null)
                    {
                        var allOrders = apiResponse.Data;
                        
                        // Calculate totals first
                        TotalOrderCount = allOrders.Count;
                        TotalRentalOrders = allOrders.Count(o => o.TransactionType == "rental");
                        TotalPurchaseOrders = allOrders.Count(o => o.TransactionType == "purchase");
                        TotalRevenue = allOrders.Sum(o => o.TotalAmount);
                        TotalRentalRevenue = allOrders.Where(o => o.TransactionType == "rental").Sum(o => o.TotalAmount);
                        TotalPurchaseRevenue = allOrders.Where(o => o.TransactionType == "purchase").Sum(o => o.TotalAmount);
                        
                        // Filter
                        var ordersFiltered = allOrders.AsEnumerable();
                        if (!string.IsNullOrWhiteSpace(SearchOrder))
                        {
                            ordersFiltered = ordersFiltered.Where(o => o.OrderCode.Contains(SearchOrder, StringComparison.OrdinalIgnoreCase) ||
                                                       o.CustomerName.Contains(SearchOrder, StringComparison.OrdinalIgnoreCase) ||
                                                       o.ProviderName.Contains(SearchOrder, StringComparison.OrdinalIgnoreCase));
                        }
                        if (filterStatus != "all")
                        {
                            if (Enum.TryParse<BusinessObject.Enums.OrderStatus>(filterStatus, true, out var status))
                            {
                                ordersFiltered = ordersFiltered.Where(o => o.Status == status);
                            }
                        }
                        if (SelectedTab == "rental")
                        {
                            ordersFiltered = ordersFiltered.Where(o => o.TransactionType == "rental");
                        }
                        else if (SelectedTab == "purchase")
                        {
                            ordersFiltered = ordersFiltered.Where(o => o.TransactionType == "purchase");
                        }
                        // If SelectedTab == "all", don't filter by transaction type

                        // Apply pagination
                        TotalItems = ordersFiltered.Count();
                        TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
                        Orders = ordersFiltered
                            .Skip((CurrentPage - 1) * PageSize)
                            .Take(PageSize)
                            .ToList();
                    }
                    else
                    {
                        ErrorMessage = "API returned null data";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"API Error: {response.StatusCode} - {errorContent}";
                    Orders = new List<AdminOrderListDto>();
                    TotalOrderCount = TotalRentalOrders = TotalPurchaseOrders = 0;
                    TotalRevenue = TotalRentalRevenue = TotalPurchaseRevenue = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order administration page");
                ErrorMessage = $"Exception: {ex.Message}";
                Orders = new List<AdminOrderListDto>();
                TotalOrderCount = TotalRentalOrders = TotalPurchaseOrders = 0;
                TotalRevenue = TotalRentalRevenue = TotalPurchaseRevenue = 0;
            }
            return Page();
        }
    }
}
