using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Dynamic;
using System.Text.Json.Nodes;

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

        // Thay đổi List<ProductDto> thành List<dynamic>
        public List<dynamic> TopRentals { get; set; } = new List<dynamic>();

        public async Task OnGetAsync()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            // 1. Truy vấn sản phẩm ưu tiên
            var topTierUrl = "odata/products" +
                             "?$filter=IsPromoted eq true and AverageRating gt 4.0" +
                             "&$orderby=RentCount desc" +
                             "&$expand=Images($filter=IsPrimary eq true)" +
                             "&$select=Id,Name,PricePerDay,AverageRating,RentCount,Images";

            await FetchAndProcessProducts(client, topTierUrl);

            // 2. Nếu chưa đủ 4 sản phẩm, truy vấn để lấy phần còn lại
            if (TopRentals.Count < 4)
            {
                int needed = 4 - TopRentals.Count;

                var regularTierUrl = "odata/products" +
                                     "?$filter=not (IsPromoted eq true and AverageRating gt 4.0)" +
                                     "&$orderby=RentCount desc" +
                                     $"&$top={needed}" +
                                     "&$expand=Images($filter=IsPrimary eq true)" +
                                     "&$select=Id,Name,PricePerDay,AverageRating,RentCount,Images";

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
                    var content = await response.Content.ReadAsStringAsync();

                    // Phân tích JSON mà không cần DTO
                    var odataNode = JsonNode.Parse(content);

                    // OData trả về một mảng trong thuộc tính "value"
                    var productNodes = odataNode?["value"]?.AsArray();

                    if (productNodes != null)
                    {
                        foreach (var productNode in productNodes)
                        {
                            if (productNode == null) continue;

                            // Tạo một đối tượng dynamic để chứa dữ liệu
                            dynamic rental = new ExpandoObject();
                            rental.Name = productNode["Name"]?.GetValue<string>();
                            rental.PricePerDay = productNode["PricePerDay"]?.GetValue<decimal>();
                            rental.AverageRating = productNode["AverageRating"]?.GetValue<decimal>();
                            rental.RentCount = productNode["RentCount"]?.GetValue<int>();
                            rental.Id = productNode["Id"]?.GetValue<Guid>() ?? Guid.Empty;

                            // Lấy ảnh chính từ mảng Images lồng nhau
                            rental.Image = productNode["Images"]?
                                           .AsArray()?
                                           .FirstOrDefault()?["ImageUrl"]?
                                           .GetValue<string>() ?? "default-image-url.jpg";

                            TopRentals.Add(rental);
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
