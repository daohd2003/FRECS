using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Provider
{
    public class ProductsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductsModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public class ODataApiResponse
        {
            [JsonPropertyName("@odata.count")]
            public int Count { get; set; }

            [JsonPropertyName("value")]
            public List<ProductDTO> Value { get; set; }
        }

        public class ProductStats
        {
            public int TotalItems { get; set; }
            public int ActiveItems { get; set; }
            public int TotalBookings { get; set; }
            public int TotalSales { get; set; }
        }

        // Properties for the page
        public List<ProductDTO> Products { get; set; } = new List<ProductDTO>();
        public ProductStats Stats { get; set; } = new ProductStats();
        public int TotalProductsCount { get; set; }

        // Filter and pagination properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        public int TotalPages => (int)Math.Ceiling((double)TotalProductsCount / PageSize);

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All Status";

        [BindProperty(SupportsGet = true)]
        public string CategoryFilter { get; set; } = "All Categories";

        [BindProperty(SupportsGet = true)]
        public string TypeFilter { get; set; } = "All Types";

        // Filter options
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "All Status", Text = "All Status" },
            new SelectListItem { Value = "Active", Text = "Active" },
            new SelectListItem { Value = "Inactive", Text = "Inactive" },
            new SelectListItem { Value = "Pending", Text = "Pending" },
            new SelectListItem { Value = "Archived", Text = "Archived" }
        };

        public List<SelectListItem> TypeOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "All Types", Text = "All Types" },
            new SelectListItem { Value = "Rental", Text = "For Rent" },
            new SelectListItem { Value = "Purchase", Text = "For Sale" },
            new SelectListItem { Value = "Both", Text = "Rent & Sale" }
        };

        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
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

                // Load categories for filter dropdown
                await LoadCategoriesAsync(client);

                // Load provider's products (with pagination)
                await LoadProviderProductsAsync(client, userId);

                // Calculate stats from ALL products (not just current page)
                await CalculateStatsAsync(client, userId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading data: {ex.Message}";
            }

            return Page();
        }

        private async Task LoadCategoriesAsync(HttpClient client)
        {
            try
            {
                var categoryResponse = await client.GetAsync("api/categories");
                if (categoryResponse.IsSuccessStatusCode)
                {
                    var categories = await categoryResponse.Content.ReadFromJsonAsync<IEnumerable<CategoryDto>>();
                    CategoryOptions = new List<SelectListItem> { new SelectListItem { Value = "All Categories", Text = "All Categories" } };
                    if (categories != null)
                    {
                        CategoryOptions.AddRange(categories
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.Name)
                            .Select(c => new SelectListItem { Value = c.Name, Text = c.Name }));
                    }
                }
            }
            catch
            {
                CategoryOptions = new List<SelectListItem> { new SelectListItem { Value = "All Categories", Text = "All Categories" } };
            }
        }

        private async Task LoadProviderProductsAsync(HttpClient client, string userId)
        {
            var queryOptions = new List<string> { "$count=true" };
            var filters = new List<string>();

            // Filter by provider
            filters.Add($"ProviderId eq {userId}");

            // Apply search filter
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                filters.Add($"(contains(tolower(Name), '{SearchQuery.ToLower()}') or contains(tolower(Description), '{SearchQuery.ToLower()}'))");
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All Status")
            {
                switch (StatusFilter)
                {
                    case "Active":
                        filters.Add("AvailabilityStatus eq 'available'");
                        break;
                    case "Inactive":
                        filters.Add("AvailabilityStatus eq 'unavailable'");
                        break;
                    case "Pending":
                        filters.Add("AvailabilityStatus eq 'pending'");
                        break;
                    case "Archived":
                        filters.Add("AvailabilityStatus eq 'archived'");
                        break;
                }
            }

            // Apply category filter
            if (!string.IsNullOrEmpty(CategoryFilter) && CategoryFilter != "All Categories")
            {
                filters.Add($"Category eq '{CategoryFilter}'");
            }

            // Apply type filter (rental/purchase)
            if (!string.IsNullOrEmpty(TypeFilter) && TypeFilter != "All Types")
            {
                switch (TypeFilter)
                {
                    case "Rental":
                        filters.Add("RentalStatus eq 'Available'");
                        break;
                    case "Purchase":
                        filters.Add("PurchaseStatus eq 'Available'");
                        break;
                    case "Both":
                        filters.Add("(RentalStatus eq 'Available' and PurchaseStatus eq 'Available')");
                        break;
                }
            }

            if (filters.Any())
            {
                queryOptions.Add($"$filter={string.Join(" and ", filters)}");
            }

            // Add ordering: Active items first (available, pending), then others (archived), sorted by CreatedAt desc within each group
            queryOptions.Add("$orderby=AvailabilityStatus eq 'available' desc, AvailabilityStatus eq 'pending' desc, AvailabilityStatus eq 'unavailable' desc, CreatedAt desc");

            // Add pagination
            queryOptions.Add($"$top={PageSize}");
            queryOptions.Add($"$skip={(CurrentPage - 1) * PageSize}");

            string queryString = string.Join("&", queryOptions);
            var requestUri = $"odata/products?{queryString}";

            var response = await client.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                var odataResponse = await response.Content.ReadFromJsonAsync<ODataApiResponse>();
                if (odataResponse != null)
                {
                    Products = odataResponse.Value ?? new List<ProductDTO>();
                    TotalProductsCount = odataResponse.Count;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = $"Error loading products: {errorContent}";
            }
        }

        private async Task CalculateStatsAsync(HttpClient client, string userId)
        {
            try
            {
                // Fetch ALL products for this provider (no pagination, no filters except providerId)
                var queryOptions = new List<string>
                {
                    "$count=true",
                    $"$filter=ProviderId eq {userId}"
                };

                string queryString = string.Join("&", queryOptions);
                var requestUri = $"odata/products?{queryString}";

                var response = await client.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var odataResponse = await response.Content.ReadFromJsonAsync<ODataApiResponse>();
                    if (odataResponse?.Value != null)
                    {
                        var allProducts = odataResponse.Value;
                        
                        // Calculate stats from ALL products
                        Stats.TotalItems = odataResponse.Count;
                        Stats.ActiveItems = allProducts.Count(p => p.AvailabilityStatus == "available");
                        Stats.TotalBookings = allProducts.Sum(p => p.RentCount);
                        Stats.TotalSales = allProducts.Sum(p => p.BuyCount);
                    }
                }
            }
            catch (Exception)
            {
                // If stats calculation fails, use default values (already initialized)
                Stats.TotalItems = TotalProductsCount;
            }
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(string productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();

                // Get the product first
                var product = Products.FirstOrDefault(p => p.Id.ToString() == productId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage();
                }

                // Toggle availability status
                var newStatus = product.AvailabilityStatus == "available" ? "unavailable" : "available";

                var updateData = new { AvailabilityStatus = newStatus };
                var content = new StringContent(JsonSerializer.Serialize(updateData), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"api/products/{productId}", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Product status updated successfully.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Error updating product status: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating product status: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteProductAsync(string productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"api/products/{productId}");

                if (response.IsSuccessStatusCode)
                {
                    // Parse response để lấy action type
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent);
                        var action = apiResponse?.Data ?? "Deleted";
                        
                        switch (action)
                        {
                            case "Archived":
                                TempData["SuccessMessage"] = "Product has been archived due to existing rental history.";
                                break;
                            case "Soft Deleted":
                                TempData["SuccessMessage"] = "Product has been deleted but kept for history.";
                                break;
                            case "Permanently Deleted":
                                TempData["SuccessMessage"] = "Product has been permanently removed.";
                                break;
                            default:
                                TempData["SuccessMessage"] = "Product deleted successfully.";
                                break;
                        }
                    }
                    catch
                    {
                        TempData["SuccessMessage"] = "Product deleted successfully.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent);
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Error deleting product.";
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = $"Error deleting product: {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting product: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRestoreProductAsync(string productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await GetAuthenticatedClientAsync();
                
                // Use dedicated restore endpoint
                var response = await client.PutAsync($"api/products/restore/{productId}", null);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseContent);
                        TempData["SuccessMessage"] = apiResponse?.Message ?? "Product restored successfully.";
                    }
                    catch
                    {
                        TempData["SuccessMessage"] = "Product has been restored to active status.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent);
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Error restoring product.";
                    }
                    catch
                    {
                        TempData["ErrorMessage"] = $"Error restoring product: {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error restoring product: {ex.Message}";
            }

            return RedirectToPage();
        }

        public class ApiResponse<T>
        {
            public string Message { get; set; }
            public T Data { get; set; }
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
    }
}

