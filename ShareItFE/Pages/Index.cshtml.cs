using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusinessObject.DTOs.ProductDto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(ILogger<IndexModel> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public class ODataApiResponse
        {
            [JsonPropertyName("@odata.count")]
            public int Count { get; set; }

            [JsonPropertyName("value")]
            public List<ProductDTO> Value { get; set; }
        }

        // Changed back to List<ProductDTO> for proper method support
        public List<ProductDTO> TopRentals { get; set; } = new List<ProductDTO>();

        public async Task OnGetAsync()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            // 1. Truy vấn sản phẩm ưu tiên
            var topTierUrl = "odata/products" +
                             "?$filter=IsPromoted eq true and AverageRating gt 4.0" +
                             "&$orderby=RentCount desc" +
                             "&$expand=Images($filter=IsPrimary eq true)";

            await FetchAndProcessProducts(client, topTierUrl);

            // 2. Nếu chưa đủ 4 sản phẩm, truy vấn để lấy phần còn lại
            if (TopRentals.Count < 4)
            {
                int needed = 4 - TopRentals.Count;

                var regularTierUrl = "odata/products" +
                                     "?$filter=not (IsPromoted eq true and AverageRating gt 4.0)" +
                                     "&$orderby=RentCount desc" +
                                     $"&$top={needed}" +
                                     "&$expand=Images($filter=IsPrimary eq true)";

                await FetchAndProcessProducts(client, regularTierUrl);
            }
        }

        // Helper method để tránh lặp code
        private async Task FetchAndProcessProducts(HttpClient client, string url)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var odataResponse = await response.Content.ReadFromJsonAsync<ODataApiResponse>(jsonOptions);
                    
                    if (odataResponse?.Value != null)
                    {
                        foreach (var product in odataResponse.Value)
                        {
                            // Ensure primary image is available for display
                            if (product.Images != null && product.Images.Any())
                            {
                                product.Images = product.Images
                                    .OrderByDescending(i => i.IsPrimary)
                                    .ToList();
                            }
                            
                            TopRentals.Add(product);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi và xử lý API từ URL: {ApiUrl}", url);
            }
        }
    }
}
