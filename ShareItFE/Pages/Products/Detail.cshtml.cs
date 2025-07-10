using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.ProfileDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
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

        public ProductDTO Product { get; set; }
        public List<FeedbackDto> Feedbacks { get; set; } = new List<FeedbackDto>();
        public bool IsFavorite { get; set; }

        [BindProperty, Required(ErrorMessage = "Please select a size")]
        public string SelectedSize { get; set; }

        [BindProperty, Required(ErrorMessage = "Please select a start date")]
        public string StartDate { get; set; }

        [BindProperty]
        public int RentalDays { get; set; } = 3;

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

            if (!ModelState.IsValid)
            {
                var feedbacksResponse = await client.GetAsync($"api/feedbacks/0/{id}");
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
                return Page();
            }

            // TODO: Handle cart logic
            return RedirectToPage("/Cart");
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

    }
}