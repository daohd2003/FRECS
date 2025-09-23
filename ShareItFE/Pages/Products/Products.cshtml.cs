using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Products
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

        public List<ProductDTO> Products { get; set; } = new List<ProductDTO>();
        public int TotalProductsCount { get; set; }
        public List<string> FavoriteProductIds { get; set; } = new List<string>(); // Thêm thuộc tính để lưu danh sách productId yêu thích

        // Filter option sources
        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();

        // Pagination properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 8;

        public int TotalPages => (int)Math.Ceiling((double)TotalProductsCount / PageSize);

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "popular";

        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "grid";

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PriceRangeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SizeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RatingFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? GenderFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ProductTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsFilterOpen { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var previousFilterState = TempData["FilterState"]?.ToString();
                var currentFilterState = JsonSerializer.Serialize(new
                {
                    SearchQuery,
                    SortBy,
                    CategoryFilter,
                    PriceRangeFilter,
                    SizeFilter,
                    RatingFilter,
                    GenderFilter,
                    ProductTypeFilter
                });

                if (previousFilterState != currentFilterState)
                {
                    CurrentPage = 1;
                }

                TempData["FilterState"] = currentFilterState;

                var client = _httpClientFactory.CreateClient("BackendApi");

                // Load categories for filter dropdown
                try
                {
                    var categoryResponse = await client.GetAsync("api/categories");
                    if (categoryResponse.IsSuccessStatusCode)
                    {
                        var categories = await categoryResponse.Content.ReadFromJsonAsync<IEnumerable<CategoryDto>>();
                        CategoryOptions = new List<SelectListItem> { new SelectListItem { Value = string.Empty, Text = "All Categories" } };
                        if (categories != null)
                        {
                            CategoryOptions.AddRange(categories
                                .Where(c => c.IsActive)
                                .OrderBy(c => c.Name)
                                .Select(c => new SelectListItem { Value = c.Name, Text = c.Name }));
                        }
                    }
                    else
                    {
                        CategoryOptions = new List<SelectListItem> { new SelectListItem { Value = string.Empty, Text = "All Categories" } };
                    }
                }
                catch
                {
                    CategoryOptions = new List<SelectListItem> { new SelectListItem { Value = string.Empty, Text = "All Categories" } };
                }

                var queryOptions = new List<string> { "$count=true" };
                var filters = new List<string>();

                Console.WriteLine($"SearchQuery: {SearchQuery}, CategoryFilter: {CategoryFilter}, PriceRangeFilter: {PriceRangeFilter}, SizeFilter: {SizeFilter}, RatingFilter: {RatingFilter}, GenderFilter: {GenderFilter}, ProductTypeFilter: {ProductTypeFilter}");
                filters.Add("AvailabilityStatus eq 'available'");
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    filters.Add($"(contains(tolower(Name), '{SearchQuery.ToLower()}') or contains(tolower(Description), '{SearchQuery.ToLower()}'))");
                }

                if (!string.IsNullOrEmpty(CategoryFilter))
                {
                    filters.Add($"Category eq '{CategoryFilter}'");
                }

                if (!string.IsNullOrEmpty(SizeFilter))
                {
                    filters.Add($"contains(Size, '{SizeFilter}')");
                }

                if (!string.IsNullOrEmpty(RatingFilter) && decimal.TryParse(RatingFilter, NumberStyles.Any, CultureInfo.InvariantCulture, out var minRating))
                {
                    filters.Add($"AverageRating ge {minRating}");
                }

                if (!string.IsNullOrEmpty(PriceRangeFilter))
                {
                    if (PriceRangeFilter.Contains('-'))
                    {
                        var parts = PriceRangeFilter.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[0], out var minPrice) && int.TryParse(parts[1], out var maxPrice))
                        {
                            filters.Add($"PricePerDay ge {minPrice} and PricePerDay le {maxPrice}");
                        }
                    }
                    else if (int.TryParse(PriceRangeFilter, out var startPrice))
                    {
                        filters.Add($"PricePerDay ge {startPrice}");
                    }
                }

                // Gender filter
                if (!string.IsNullOrEmpty(GenderFilter))
                {
                    filters.Add($"Gender eq '{GenderFilter}'");
                }

                // Product Type filter (Rental/Purchase)
                if (!string.IsNullOrEmpty(ProductTypeFilter))
                {
                    if (ProductTypeFilter == "Rental")
                    {
                        filters.Add("RentalStatus eq 'Available'");
                    }
                    else if (ProductTypeFilter == "Purchase")
                    {
                        filters.Add("PurchaseStatus eq 'Available'");
                    }
                }

                if (filters.Any())
                {
                    queryOptions.Add($"$filter={string.Join(" and ", filters)}");
                }

                if (!string.IsNullOrEmpty(SortBy))
                {
                    switch (SortBy)
                    {
                        case "price-low":
                            queryOptions.Add("$orderby=PricePerDay asc");
                            break;
                        case "price-high":
                            queryOptions.Add("$orderby=PricePerDay desc");
                            break;
                        case "rating":
                            queryOptions.Add("$orderby=AverageRating desc");
                            break;
                        case "popular":
                        default:
                            queryOptions.Add("$orderby=RentCount desc");
                            break;
                    }
                }

                queryOptions.Add($"$top={PageSize}");
                queryOptions.Add($"$skip={(CurrentPage - 1) * PageSize}");

                string queryString = string.Join("&", queryOptions);
                var requestUri = $"odata/products?{queryString}";
                Console.WriteLine($"Request URI: {requestUri}");

                var response = await client.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var odataResponse = await response.Content.ReadFromJsonAsync<ODataApiResponse>();
                    if (odataResponse != null)
                    {
                        Products = odataResponse.Value ?? new List<ProductDTO>();
                        TotalProductsCount = odataResponse.Count;
                        Console.WriteLine($"Fetched {Products.Count} products, Total Count: {TotalProductsCount}");
                    }
                    else
                    {
                        Console.WriteLine("OData response is null");
                        TempData["ErrorMessage"] = "Unable to read data from API.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching products. Status code: {response.StatusCode}, Content: {errorContent}");
                    TempData["ErrorMessage"] = $"Error loading products: {errorContent}";
                }

                // Kiểm tra danh sách yêu thích từ API
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                        if (!string.IsNullOrEmpty(authToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                            var favoriteUri = $"api/favorites/{userId}"; // Sử dụng path parameter
                            var favoriteResponse = await client.GetAsync(favoriteUri);
                            if (favoriteResponse.IsSuccessStatusCode)
                            {
                                var apiResponse = await favoriteResponse.Content.ReadFromJsonAsync<ApiResponse<List<FavoriteCreateDto>>>();
                                if (apiResponse != null && apiResponse.Data != null)
                                {
                                    FavoriteProductIds = apiResponse.Data.Select(f => f.ProductId.ToString()).ToList();
                                }
                            }
                            else
                            {
                                var errorContent = await favoriteResponse.Content.ReadAsStringAsync();
                                TempData["ErrorMessage"] = $"Error loading favorites: {errorContent}";
                            }
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Authentication token not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Server error: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddFavoriteAsync(string productId)
        {
            // Check if this is an AJAX request
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                                Request.Headers.ContainsKey("RequestVerificationToken");

            if (!User.Identity.IsAuthenticated)
            {
                if (isAjaxRequest)
                {
                    return new JsonResult(new { success = false, message = "User not authenticated" }) { StatusCode = 401 };
                }
                return RedirectToPage("/Auth", new { returnUrl = "/products" });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                if (isAjaxRequest)
                {
                    return new JsonResult(new { success = false, message = "User is not logged in" }) { StatusCode = 400 };
                }
                TempData["ErrorMessage"] = "User is not logged in.";
                return RedirectToPage();
            }

            try
            {
                var client = _httpClientFactory.CreateClient("BackendApi");
                var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(authToken))
                {
                    if (isAjaxRequest)
                    {
                        return new JsonResult(new { success = false, message = "Authentication token not found" }) { StatusCode = 401 };
                    }
                    TempData["ErrorMessage"] = "Authentication token not found.";
                    return RedirectToPage();
                }
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                var favoriteCheckUri = $"api/favorites/check?userId={userId}&productId={productId}";
                var checkResponse = await client.GetAsync(favoriteCheckUri);

                if (checkResponse.IsSuccessStatusCode)
                {
                    var isFavoritedResponse = await checkResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    if (isFavoritedResponse != null && isFavoritedResponse.Data)
                    {
                        // Đoạn này gọi API để xóa sản phẩm khỏi danh sách yêu thích
                        var deleteFavoriteUri = $"api/favorites?userId={userId}&productId={productId}";
                        var deleteResponse = await client.DeleteAsync(deleteFavoriteUri);

                        if (deleteResponse.IsSuccessStatusCode)
                        {
                            if (isAjaxRequest)
                            {
                                return new JsonResult(new { success = true, action = "removed", message = "Removed from favorites" });
                            }
                            TempData["FavoriteAction"] = "removed";
                            TempData["LastActionProductId"] = productId;
                            return RedirectToPage();
                        }
                        else
                        {
                            var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                            if (isAjaxRequest)
                            {
                                return new JsonResult(new { success = false, message = $"Error removing from favorites: {errorContent}" }) { StatusCode = 400 };
                            }
                            TempData["ErrorMessage"] = $"Error removing from favorites: {errorContent}";
                            return RedirectToPage();
                        }
                    }
                }

                var favoriteData = new { UserId = userId, ProductId = productId };
                var content = new StringContent(JsonSerializer.Serialize(favoriteData), Encoding.UTF8, "application/json");
                var favoriteAddUri = "api/favorites";
                var addResponse = await client.PostAsync(favoriteAddUri, content);

                if (addResponse.IsSuccessStatusCode)
                {
                    if (isAjaxRequest)
                    {
                        return new JsonResult(new { success = true, action = "added", message = "Added to favorites successfully" });
                    }
                    TempData["LastAddedProductId"] = productId.ToString();
                    return RedirectToPage();
                }
                else
                {
                    var errorContent = await addResponse.Content.ReadAsStringAsync();
                    if (isAjaxRequest)
                    {
                        return new JsonResult(new { success = false, message = $"Error adding to favorites: {errorContent}" }) { StatusCode = 400 };
                    }
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                if (isAjaxRequest)
                {
                    return new JsonResult(new { success = false, message = $"Server error: {ex.Message}" }) { StatusCode = 500 };
                }
                TempData["ErrorMessage"] = $"Server error: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}