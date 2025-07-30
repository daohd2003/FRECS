using Azure;
using Azure.Core;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.ProfileDtos;
using BusinessObject.Enums;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Products
{
    public class DetailModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _configuration;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        public DetailModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, AuthenticatedHttpClientHelper clientHelper)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _configuration = configuration;
            _clientHelper = clientHelper;
        }

        public ProductDTO Product { get; set; }
        public List<FeedbackDto> Feedbacks { get; set; } = new List<FeedbackDto>();
        public bool IsFavorite { get; set; }

        [BindProperty, Required(ErrorMessage = "Please select a size")]
        public string SelectedSize { get; set; }

        [BindProperty, Required(ErrorMessage = "Please select a start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [BindProperty]
        public int RentalDays { get; set; } = 3;

        public Guid? CurrentUserId { get; set; }
        public string ApiBaseUrl { get; private set; }
        public string SignalRRootUrl { get; private set; }
        public string? AccessToken { get; private set; }

        [TempData] // Dùng TempData để hiển thị thông báo sau khi chuyển hướng
        public string? SuccessMessage { get; set; }

        [TempData] // Dùng TempData để hiển thị thông báo lỗi
        public string? ErrorMessage { get; set; }



        //---------------------Hau------------------------------------
        [BindProperty]
        public FeedbackRequest FeedbackInput { get; set; } = new FeedbackRequest();

        public async Task<IActionResult> OnPostAddFeedbackAsync(Guid id)
        {
            Guid orderItemId;
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];

            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(AccessToken))
            {
                ErrorMessage = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.";
                await LoadInitialData(id);
                return Page();
            }


            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var requestURI = $"https://localhost:7256/api/orders/order-item/{id}";
            var orderItemresponse = await client.GetAsync(requestURI);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };


            if (orderItemresponse.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<String>>(   await orderItemresponse.Content.ReadAsStringAsync(), options);

                string resutl = apiResponse?.Data;

                if (resutl != null) { 
                    orderItemId = Guid.Parse(resutl);
                }
                else
                {
                    orderItemId = Guid.Empty;
                    Console.WriteLine($"User need to rent it to use feedback function");
                    return RedirectToPage(new { id = id });
                }

            }
            else
            {
                var errorContent = await orderItemresponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Error fetching");
                return RedirectToPage(new { id = id });
            }

            FeedbackInput.TargetId = id;
            //FeedbackInput.OrderItemId = "4B4816CE-70A3-4BDB-BA1A-03D2080F2623";
            //string input = "7361EEA2-5453-41B7-9999-11482B46E57B";
            FeedbackInput.OrderItemId = orderItemId;
            // 4. Make API Call
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

                // Assuming your API endpoint for submitting feedback is "/api/feedback"
                var apiUrl = $"https://localhost:7256/api/feedbacks"; // Make sure _apiBaseUrl is configured

                var response = await httpClient.PostAsJsonAsync(apiUrl, FeedbackInput);

                if (response.IsSuccessStatusCode)
                {
                    // API returned 201 Created (or 200 OK)
                    TempData["SuccessMessage"] = "Phản hồi của bạn đã được gửi thành công!";
                    SuccessMessage = "Phản hồi của bạn đã được gửi thành công!";
                    // Optional: You might want to reload product details to show the new feedback immediately
                    // await LoadInitialData(id);
                }
                else
                {
                    // API returned an error status code
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Lỗi khi gửi phản hồi: {response.StatusCode} - {errorContent}";
                    // Log the detailed errorContent for debugging
                    Console.WriteLine($"Feedback API Error: {response.StatusCode} - {errorContent}");
                    // Reload page data to maintain state if an error occurs
                    // await LoadInitialData(id);
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle network errors or API not reachable
                TempData["ErrorMessage"] = $"Không thể kết nối đến dịch vụ: {ex.Message}";
                Console.WriteLine($"HttpRequestException: {ex.Message}");
                // await LoadInitialData(id);
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                TempData["ErrorMessage"] = $"Đã xảy ra lỗi không mong muốn: {ex.Message}";
                Console.WriteLine($"General Exception: {ex.Message}");
                // await LoadInitialData(id);
            }

            // Always redirect after a POST to prevent "Resubmit form" warnings on refresh
            // and to clear the form data.
            return RedirectToPage(new { id = id });
        }


        //---------------------Hau------------------------------------





        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            if (StartDate == DateTime.MinValue)
            {
                StartDate = DateTime.Today;
            }

            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

            if (id == Guid.Empty) return BadRequest("Invalid product ID.");

            var client = _httpClientFactory.CreateClient("BackendApi");
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];
            var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(authToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            try
            {
                // Fetch product details
                var productRequestUri = $"api/products/{id}";
                var productResponse = await client.GetAsync(productRequestUri);

                if (productResponse.IsSuccessStatusCode)
                {
                    Product = await productResponse.Content.ReadFromJsonAsync<ProductDTO>(_jsonOptions);
                    if (Product != null && Product.Images != null)
                    {
                        Product.Images = Product.Images
                            .OrderByDescending(i => i.IsPrimary) // ảnh chính lên đầu
                            .ToList();
                    }
                    if (Product == null) return NotFound();
                }
                else
                {
                    var errorContent = await productResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching product. Status: {productResponse.StatusCode}. URI: {productRequestUri}. Content: {errorContent}");
                    return NotFound();
                }

                // Fetch feedbacks
                var feedbacksRequestUri = $"api/feedbacks/0/{id}";
                var feedbacksResponse = await client.GetAsync(feedbacksRequestUri);

                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await feedbacksResponse.Content.ReadFromJsonAsync<ApiResponse<List<FeedbackDto>>>(_jsonOptions);
                    Feedbacks = apiResponse?.Data ?? new List<FeedbackDto>();

                    foreach (var feedback in Feedbacks)
                    {
                        var profileRequestUri = $"api/profile/{feedback.CustomerId}";
                        var profileResponse = await client.GetAsync(profileRequestUri);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var apiResponseProfile = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserHeaderInfoDto>>(_jsonOptions);
                            var profile = apiResponseProfile?.Data;
                            feedback.ProfilePictureUrl = profile?.ProfilePictureUrl ?? "https://via.placeholder.com/40.png?text=No+Image";
                        }
                        else
                        {
                            feedback.ProfilePictureUrl = "https://via.placeholder.com/40.png?text=No+Image";
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Could not fetch feedbacks. Status: {feedbacksResponse.StatusCode}");
                }

                // Check favorite status
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var favoriteUri = $"api/favorites/{userId}";
                        var favoriteResponse = await client.GetAsync(favoriteUri);
                        if (favoriteResponse.IsSuccessStatusCode)
                        {
                            var apiResponse = await favoriteResponse.Content.ReadFromJsonAsync<ApiResponse<List<FavoriteCreateDto>>>(_jsonOptions);
                            if (apiResponse != null && apiResponse.Data != null)
                            {
                                IsFavorite = apiResponse.Data.Any(f => f.ProductId == id);
                            }
                        }
                    }
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
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];

            // 1. Kiểm tra xác thực người dùng
            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(AccessToken))
            {
                ErrorMessage = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.";
                await LoadInitialData(id); // Tải lại dữ liệu để giữ trạng thái trang
                return Page();
            }

            // 2. Kiểm tra ModelState.IsValid (Validation từ thuộc tính)
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại kích thước và ngày thuê.";
                await LoadInitialData(id); // Tải lại dữ liệu để hiển thị trang
                return Page();
            }

            // 3. Chuẩn bị dữ liệu và gọi API
            var cartAddRequestDto = new CartAddRequestDto
            {
                ProductId = id,
                Size = SelectedSize,
                RentalDays = RentalDays,
                StartDate = StartDate,
                Quantity = 1 // Mặc định số lượng là 1 từ trang chi tiết
            };
            Console.WriteLine($"Adding to cart: ProductId={id}, StartDate={StartDate}, RentalDays={RentalDays}");
            var client = _httpClientFactory.CreateClient("BackendApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

            try
            {
                var response = await client.PostAsJsonAsync("api/cart", cartAddRequestDto);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Sản phẩm đã được thêm vào giỏ hàng!";
                    return RedirectToPage("/CartPage/Cart");
                }
                else
                {
                    // Đọc lỗi từ API để hiển thị thông báo chính xác hơn
                    var errorContent = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(_jsonOptions);
                    ErrorMessage = errorContent?.Message ?? "Không thể thêm vào giỏ hàng. Vui lòng thử lại.";
                    await LoadInitialData(id);
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
                await LoadInitialData(id);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAddFavoriteAsync(string productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth", new { returnUrl = $"/products/detail/{productId}" });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User is not logged in.";
                return RedirectToPage(new { id = productId });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("BackendApi");
                var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(authToken))
                {
                    TempData["ErrorMessage"] = "Authentication token not found.";
                    return RedirectToPage(new { id = productId });
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                // Kiểm tra xem sản phẩm đã được yêu thích chưa
                var checkUri = $"api/favorites/check?userId={userId}&productId={productId}";
                var checkResponse = await client.GetAsync(checkUri);

                if (checkResponse.IsSuccessStatusCode)
                {
                    var responseContent = await checkResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<bool>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    var isFavorite = result?.Data == true;

                    if (isFavorite)
                    {
                        // Nếu đã yêu thích → gửi DELETE để gỡ khỏi danh sách
                        var deleteUri = $"api/favorites?userId={userId}&productId={productId}";
                        var deleteResponse = await client.DeleteAsync(deleteUri);
                        if (deleteResponse.IsSuccessStatusCode)
                        {
                            IsFavorite = false;
                        }
                    }
                    else
                    {
                        // Nếu chưa yêu thích → gửi POST để thêm vào danh sách
                        var favoriteData = new { UserId = userId, ProductId = productId };
                        var content = new StringContent(JsonSerializer.Serialize(favoriteData), Encoding.UTF8, "application/json");
                        var addResponse = await client.PostAsync("api/favorites", content);

                        if (addResponse.IsSuccessStatusCode)
                        {
                            IsFavorite = true;
                        }
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Unable to check favorite status.";
                }

                return RedirectToPage(new { id = productId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error server: {ex.Message}";
                return RedirectToPage(new { id = productId });
            }
        }
        private async Task LoadInitialData(Guid id)
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"];
            SignalRRootUrl = _configuration["ApiSettings:RootUrl"];

            if (id == Guid.Empty) return; // Không cần xử lý BadRequest ở đây, nó đã được xử lý ở OnGetAsync

            var client = _httpClientFactory.CreateClient("BackendApi");
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];
            var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(authToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            try
            {
                // Fetch product details
                var productRequestUri = $"api/products/{id}";
                var productResponse = await client.GetAsync(productRequestUri);

                if (productResponse.IsSuccessStatusCode)
                {
                    Product = await productResponse.Content.ReadFromJsonAsync<ProductDTO>(_jsonOptions);
                    if (Product != null && Product.Images != null)
                    {
                        Product.Images = Product.Images
                            .OrderByDescending(i => i.IsPrimary) // ảnh chính lên đầu
                            .ToList();
                    }
                    if (Product == null) Product = new ProductDTO(); // Khởi tạo Product để tránh null reference
                }
                else
                {
                    // Xử lý lỗi nếu không tải được sản phẩm
                    var errorContent = await productResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching product. Status: {productResponse.StatusCode}. URI: {productRequestUri}. Content: {errorContent}");
                    Product = new ProductDTO(); // Khởi tạo Product rỗng để tránh null
                }

                // Fetch feedbacks
                var feedbacksRequestUri = $"api/feedbacks/0/{id}";
                var feedbacksResponse = await client.GetAsync(feedbacksRequestUri);

                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await feedbacksResponse.Content.ReadFromJsonAsync<ApiResponse<List<FeedbackDto>>>(_jsonOptions);
                    Feedbacks = apiResponse?.Data ?? new List<FeedbackDto>();

                    foreach (var feedback in Feedbacks)
                    {
                        var profileRequestUri = $"api/profile/{feedback.CustomerId}";
                        var profileResponse = await client.GetAsync(profileRequestUri);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var apiResponseProfile = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserHeaderInfoDto>>(_jsonOptions);
                            var profile = apiResponseProfile?.Data;
                            feedback.ProfilePictureUrl = profile?.ProfilePictureUrl ?? "https://via.placeholder.com/40.png?text=No+Image";
                        }
                        else
                        {
                            feedback.ProfilePictureUrl = "https://via.placeholder.com/40.png?text=No+Image";
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Could not fetch feedbacks. Status: {feedbacksResponse.StatusCode}");
                }

                // Check favorite status
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var favoriteUri = $"api/favorites/{userId}";
                        var favoriteResponse = await client.GetAsync(favoriteUri);
                        if (favoriteResponse.IsSuccessStatusCode)
                        {
                            var apiResponse = await favoriteResponse.Content.ReadFromJsonAsync<ApiResponse<List<FavoriteCreateDto>>>(_jsonOptions);
                            if (apiResponse != null && apiResponse.Data != null)
                            {
                                IsFavorite = apiResponse.Data.Any(f => f.ProductId == id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred in LoadInitialData: {ex.Message}");
                // Không return StatusCode ở đây, chỉ set ErrorMessage để hiển thị trên trang
                ErrorMessage = "Đã có lỗi xảy ra khi tải dữ liệu sản phẩm.";
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString))
            {
                CurrentUserId = Guid.Parse(userIdString);
            }
        }
    }
}
public class FeedbackRequest
{
    public FeedbackTargetType TargetType { get; set; }

    public Guid TargetId { get; set; } 

    public Guid? OrderItemId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }
}
