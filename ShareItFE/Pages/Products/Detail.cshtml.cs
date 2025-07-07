using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace ShareItFE.Pages.Products
{
    public class DetailModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _configuration;

        public DetailModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _configuration = configuration;
        }

        // Dữ liệu từ 2 API call khác nhau
        public ProductDTO Product { get; set; }
        public List<FeedbackDto> Feedbacks { get; set; } = new List<FeedbackDto>();

        // Các thuộc tính cho form, có validation
        [BindProperty, Required(ErrorMessage = "Please select a size")]
        public string SelectedSize { get; set; }

        [BindProperty, Required(ErrorMessage = "Please select a start date")]
        public string StartDate { get; set; }

        [BindProperty]
        public int RentalDays { get; set; } = 3; // Mặc định là 3 ngày

        public Guid? CurrentUserId { get; set; }
        public string ApiBaseUrl { get; private set; }
        public string SignalRRootUrl { get; private set; }
        public string? AccessToken { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

            if (id == Guid.Empty) return BadRequest("Invalid product ID.");

            var client = _httpClientFactory.CreateClient("BackendApi");
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];

            try
            {
                // --- CALL API 1: LẤY THÔNG TIN SẢN PHẨM ---
                var productRequestUri = $"api/products/{id}";
                var productResponse = await client.GetAsync(productRequestUri);

                if (productResponse.IsSuccessStatusCode)
                {
                    Product = await productResponse.Content.ReadFromJsonAsync<ProductDTO>(_jsonOptions);
                    if (Product == null) return NotFound();
                }
                else
                {
                    var errorContent = await productResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching product. Status: {productResponse.StatusCode}. URI: {productRequestUri}. Content: {errorContent}");
                    return NotFound();
                }

                // --- CALL API 2: LẤY DANH SÁCH FEEDBACK ---
                var feedbacksRequestUri = $"api/feedbacks/0/{id}";
                var feedbacksResponse = await client.GetAsync(feedbacksRequestUri);

                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await feedbacksResponse.Content.ReadFromJsonAsync<ApiResponse<List<FeedbackDto>>>(_jsonOptions);
                    Feedbacks = apiResponse?.Data ?? new List<FeedbackDto>();
                }
                else
                {
                    Console.WriteLine($"Could not fetch feedbacks. Status: {feedbacksResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred: {ex.Message}");
                return StatusCode(500, "An internal error occurred.");
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString))
            {
                CurrentUserId = Guid.Parse(userIdString);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAddToCartAsync(Guid id)
        {
            // Tải lại thông tin sản phẩm để đảm bảo tính toàn vẹn
            var client = _httpClientFactory.CreateClient("BackendApi");
            var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(authToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            var productRequestUri = $"api/products/{id}";
            var productResponse = await client.GetAsync(productRequestUri);

            if (!productResponse.IsSuccessStatusCode) return NotFound();

            Product = await productResponse.Content.ReadFromJsonAsync<ProductDTO>(_jsonOptions);
            if (Product == null) return NotFound();

            // Kiểm tra validation từ server
            if (!ModelState.IsValid)
            {
                // Nếu có lỗi, cần lấy lại feedbacks để hiển thị lại trang cho đúng
                var feedbacksResponse = await client.GetAsync($"api/feedbacks/0/{id}");
                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await feedbacksResponse.Content.ReadFromJsonAsync<ApiResponse<List<FeedbackDto>>>(_jsonOptions);
                    Feedbacks = apiResponse?.Data ?? new List<FeedbackDto>();
                }
                return Page();
            }

            // TODO: Xử lý logic thêm vào giỏ hàng của bạn tại đây
            // Ví dụ: Lưu vào session hoặc database
            // ...

            return RedirectToPage("/Cart");
        }
    }
}