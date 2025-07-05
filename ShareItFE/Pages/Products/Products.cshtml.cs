using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
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

        // --- SỬA ĐỔI: Dọn dẹp thuộc tính, chỉ giữ lại các thuộc tính đang được form sử dụng ---
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "popular";

        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "grid";

        // Các thuộc tính mới cho bộ lọc chi tiết
        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PriceRangeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SizeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RatingFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsFilterOpen { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BackendApi");
                /*
                var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                if (!string.IsNullOrEmpty(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                }
                */

                var queryOptions = new List<string> { "$count=true" };
                var filters = new List<string>();

                // --- SỬA ĐỔI HOÀN TOÀN LOGIC XÂY DỰNG FILTER ---
                // 1. Filter theo SearchQuery
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    filters.Add($"(contains(tolower(Name), '{SearchQuery.ToLower()}') or contains(tolower(Description), '{SearchQuery.ToLower()}'))");
                }

                // 2. Filter theo Category chi tiết
                if (!string.IsNullOrEmpty(CategoryFilter))
                {
                    filters.Add($"Category eq '{CategoryFilter}'");
                }

                // 3. Filter theo Size chi tiết
                if (!string.IsNullOrEmpty(SizeFilter))
                {
                    filters.Add($"contains(Size, '{SizeFilter}')");
                }

                // 4. Filter theo Rating chi tiết
                if (!string.IsNullOrEmpty(RatingFilter) && decimal.TryParse(RatingFilter, NumberStyles.Any, CultureInfo.InvariantCulture, out var minRating))
                {
                    filters.Add($"AverageRating ge {minRating}");
                }

                // 5. Filter theo Price Range chi tiết
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
                    else if (int.TryParse(PriceRangeFilter, out var startPrice)) // Xử lý cho trường hợp "40+"
                    {
                        filters.Add($"PricePerDay ge {startPrice}");
                    }
                }

                if (filters.Any())
                {
                    queryOptions.Add($"$filter={string.Join(" and ", filters)}");
                }

                // Logic sắp xếp (giữ nguyên, đã đúng)
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
                    Console.WriteLine($"Error fetching products. Status code: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error content: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred while fetching products: {ex.Message}");
            }

            return Page();
        }
    }
}