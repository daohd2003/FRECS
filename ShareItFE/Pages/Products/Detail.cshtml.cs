using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.DTOs.ProfileDtos;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Products
{
    public class DetailModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        public DetailModel(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IWebHostEnvironment environment, AuthenticatedHttpClientHelper clientHelper)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            _configuration = configuration;
            _environment = environment;
            _clientHelper = clientHelper;
        }

        public ProductDTO Product { get; set; }
        public List<FeedbackResponseDto> Feedbacks { get; set; } = new List<FeedbackResponseDto>();
        public bool IsFavorite { get; set; }
        public string? ProviderEmail { get; set; }
        
        // Pagination properties for feedbacks
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalFeedbacks { get; set; } = 0;
        public int VisibleFeedbacks { get; set; } = 0; // Number of visible feedbacks (after filtering blocked)
        public int PageSize { get; set; } = 5;
        


        [BindProperty, Required(ErrorMessage = "Please select a size")]
        public string SelectedSize { get; set; }

        [BindProperty]
        public BusinessObject.Enums.TransactionType TransactionType { get; set; } = BusinessObject.Enums.TransactionType.rental;

        // StartDate and RentalDays moved to Cart; backend will default
        [BindProperty]
        public DateTime? StartDate { get; set; }

        [BindProperty]
        public int? RentalDays { get; set; }

        public Guid? CurrentUserId { get; set; }
        public string ApiBaseUrl { get; private set; }

        public bool CanBeFeedbacked { get; private set; }
        public bool IsOwnProduct { get; private set; } // Check if current user is the provider of this product
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

            // Clear any existing TempData to prevent confusion
            TempData.Remove("ErrorMessage");
            TempData.Remove("SuccessMessage");

            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(AccessToken))
            {
                ErrorMessage = "Please log in to submit feedback.";
                await LoadInitialData(id, 1);
                return Page();
            }


            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var requestURI = $"{_configuration.GetApiBaseUrl(_environment)}/orders/order-item/{id}";
            var orderItemresponse = await client.GetAsync(requestURI);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };


            if (orderItemresponse.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<String>>(await orderItemresponse.Content.ReadAsStringAsync(), options);

                string resutl = apiResponse?.Data;

                if (resutl != null)
                {
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
                var apiUrl = $"{_configuration.GetApiBaseUrl(_environment)}/feedbacks";

                var response = await httpClient.PostAsJsonAsync(apiUrl, FeedbackInput);

                if (response.IsSuccessStatusCode)
                {
                    // API returned 201 Created (or 200 OK)
                    TempData["SuccessMessage"] = "Your feedback has been submitted successfully!";
                    SuccessMessage = "Your feedback has been submitted successfully!";
                    // Optional: You might want to reload product details to show the new feedback immediately
                    // await LoadInitialData(id);
                }
                else
                {
                    // API returned an error status code
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Try to parse ApiResponse to get clean error message
                    try
                    {
                        var apiErrorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        TempData["ErrorMessage"] = apiErrorResponse?.Message ?? $"Error: {response.StatusCode}";
                    }
                    catch (JsonException)
                    {
                        // If parsing fails, show status code only
                        TempData["ErrorMessage"] = $"Error: {response.StatusCode}";
                    }
                    
                    // Log the detailed errorContent for debugging
                    Console.WriteLine($"Feedback API Error: {response.StatusCode} - {errorContent}");
                    // Reload page data to maintain state if an error occurs
                    // await LoadInitialData(id);
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle network errors or API not reachable
                TempData["ErrorMessage"] = $"Unable to connect to service: {ex.Message}";
                Console.WriteLine($"HttpRequestException: {ex.Message}");
                // await LoadInitialData(id);
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                Console.WriteLine($"General Exception: {ex.Message}");
                // await LoadInitialData(id);
            }

            // Always redirect after a POST to prevent "Resubmit form" warnings on refresh
            // and to clear the form data.
            return RedirectToPage(new { id = id });
        }


        //---------------------Hau------------------------------------





        public async Task<IActionResult> OnGetAsync(Guid id, [FromQuery] int page = 1)
        {
            // Read TempData messages if any (from redirects)
            if (TempData["SuccessMessage"] is string successMsg)
                SuccessMessage = successMsg;
            if (TempData["ErrorMessage"] is string errorMsg)
                ErrorMessage = errorMsg;

            // Fallback: Get page from Request.Query if [FromQuery] doesn't work
            if (page == 1 && Request.Query.ContainsKey("page"))
            {
                if (int.TryParse(Request.Query["page"], out int queryPage))
                {
                    page = queryPage;
                }
            }
            
            CurrentPage = page > 0 ? page : 1;
            try
            {
                // No-op: StartDate/RentalDays handled later in Cart

                ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
                SignalRRootUrl = _configuration.GetApiRootUrl(_environment);

                if (id == Guid.Empty) return BadRequest("Invalid product ID.");

                var client = _httpClientFactory.CreateClient("BackendApi");
                AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];
                var authToken = _httpContextAccessor.HttpContext.Request.Cookies["AccessToken"];
                if (!string.IsNullOrEmpty(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                }

                // Fetch product details
                var productRequestUri = $"api/products/{id}";
                var productResponse = await client.GetAsync(productRequestUri);

                if (productResponse.IsSuccessStatusCode)
                {
                    var apiResponse = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductDTO>>(_jsonOptions);
                    Product = apiResponse?.Data;
                    if (Product != null && Product.Images != null)
                    {
                        Product.Images = Product.Images
                            .OrderByDescending(i => i.IsPrimary) // ảnh chính lên đầu
                            .ToList();
                    }
                    if (Product == null) return NotFound();
                    
                    // Set default transaction type based on product availability
                    SetDefaultTransactionType();
                    
                    // Fetch provider email via profile endpoint
                    try
                    {
                        var profileRequestUri = $"api/profile/{Product.ProviderId}";
                        var profileResponse = await client.GetAsync(profileRequestUri);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var raw = await profileResponse.Content.ReadAsStringAsync();
                            var doc = JsonSerializer.Deserialize<JsonElement>(raw, _jsonOptions);
                            if (doc.TryGetProperty("data", out var dataEl))
                            {
                                if (dataEl.TryGetProperty("user", out var userEl) && userEl.TryGetProperty("email", out var emailEl))
                                {
                                    ProviderEmail = emailEl.GetString();
                                }
                            }
                        }
                    }
                    catch { }


                }
                else
                {
                    var errorContent = await productResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching product. Status: {productResponse.StatusCode}. URI: {productRequestUri}. Content: {errorContent}");
                    return NotFound();
                }

                // Fetch feedbacks with pagination using correct endpoint
                // Pass currentUserId to backend so it can filter blocked feedbacks properly
                var feedbacksRequestUri = $"api/feedbacks/product/{id}?page={CurrentPage}&pageSize={PageSize}";
                if (CurrentUserId.HasValue)
                {
                    feedbacksRequestUri += $"&currentUserId={CurrentUserId.Value}";
                }
                var feedbacksResponse = await client.GetAsync(feedbacksRequestUri);

                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    try
                    {
                        var responseContent = await feedbacksResponse.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(responseContent, _jsonOptions);
                        var paginatedData = apiResponse?.Data;
                        
                        if (paginatedData != null)
                        {
                            Feedbacks = paginatedData.Items ?? new List<FeedbackResponseDto>();
                            TotalFeedbacks = paginatedData.TotalItems;
                            VisibleFeedbacks = paginatedData.VisibleCount ?? paginatedData.TotalItems;
                            TotalPages = paginatedData.TotalPages;
                            
                            Console.WriteLine($"[Pagination Debug] TotalItems: {TotalFeedbacks}, PageSize: {PageSize}, TotalPages: {TotalPages}, CurrentPage: {CurrentPage}");
                        }
                        else
                        {
                            Feedbacks = new List<FeedbackResponseDto>();
                            TotalFeedbacks = 0;
                            TotalPages = 1;
                        }
                    }
                    catch (JsonException)
                    {
                        Feedbacks = new List<FeedbackResponseDto>();
                        TotalFeedbacks = 0;
                        TotalPages = 1;
                    }

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

                // Check favorite status and if user owns this product
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                    {
                        // Check if current user is the provider of this product
                        if (Product != null && Product.ProviderId == userGuid)
                        {
                            IsOwnProduct = true;
                        }

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

                // check whether a user can feedback
                CanBeFeedbacked = await CanFeedback(id);
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP Error in OnGetAsync: {httpEx.Message}");
                return NotFound("Product not found or service unavailable.");
            }
            catch (TaskCanceledException tcEx)
            {
                Console.WriteLine($"Timeout Error in OnGetAsync: {tcEx.Message}");
                return StatusCode(504, "Request timeout. Please try again.");
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON Parsing Error in OnGetAsync: {jsonEx.Message}");
                return StatusCode(500, "Error processing product data.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error in OnGetAsync: {ex}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, "An unexpected error occurred. Please try again later.");
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

            // Clear any existing TempData error messages to prevent them from appearing elsewhere
            TempData.Remove("ErrorMessage");
            TempData.Remove("SuccessMessage");

            // 1. Kiểm tra xác thực người dùng
            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(AccessToken))
            {
                ErrorMessage = "Please log in to add products to your cart.";
                await LoadInitialData(id, 1); // Tải lại dữ liệu để giữ trạng thái trang
                return Page();
            }

            // 2. Load Product data first to validate
            await LoadInitialData(id, CurrentPage);
            if (Product == null)
            {
                ErrorMessage = "Product not found.";
                return Page();
            }

            // 3. Kiểm tra ModelState.IsValid (Validation từ thuộc tính)
            // Only size required at product detail level
            ModelState.Remove(nameof(StartDate));
            ModelState.Remove(nameof(RentalDays));
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please select a size.";
                return Page();
            }

            // 4. Validate transaction type selection based on product availability
            if (TransactionType == BusinessObject.Enums.TransactionType.purchase)
            {
                if (Product.PurchaseStatus != "Available" || Product.PurchasePrice <= 0)
                {
                    ErrorMessage = "This product is not available for purchase.";
                    return Page();
                }
            }
            else // Rental
            {
                if (Product.RentalStatus != "Available" || Product.PricePerDay <= 0)
                {
                    ErrorMessage = "This product is not available for rental.";
                    return Page();
                }
            }

            // 5. Chuẩn bị dữ liệu và gọi API
            var cartAddRequestDto = new CartAddRequestDto
            {
                ProductId = id,
                Size = SelectedSize,
                TransactionType = TransactionType, // Set transaction type from user selection
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
                    TempData["SuccessMessage"] = "Product added to cart successfully!";
                    return RedirectToPage("/CartPage/Cart");
                }
                else
                {
                    // Đọc lỗi từ API để hiển thị thông báo chính xác hơn
                    var errorContent = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(_jsonOptions);
                    var errorMessage = errorContent?.Message ?? "Cannot add to cart. Please try again.";
                    
                    // If error is related to quantity/stock, redirect to cart page to show the error
                    // Otherwise, stay on product detail page
                    if (errorMessage.Contains("available") || errorMessage.Contains("stock") || errorMessage.Contains("units"))
                    {
                        TempData["ErrorMessage"] = errorMessage;
                        return RedirectToPage("/CartPage/Cart");
                    }
                    else
                    {
                        ErrorMessage = errorMessage;
                        await LoadInitialData(id, 1);
                        return Page();
                    }
                }
            }
            catch (Exception ex)
            {
                // Use the exception message directly without adding prefix
                ErrorMessage = ex.Message;
                await LoadInitialData(id, 1);
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
        private async Task LoadInitialData(Guid id, int page = 1)
        {
            CurrentPage = page > 0 ? page : 1;
            
            ApiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            SignalRRootUrl = _configuration.GetApiRootUrl(_environment);

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
                    var apiResponse = await productResponse.Content.ReadFromJsonAsync<ApiResponse<ProductDTO>>(_jsonOptions);
                    Product = apiResponse?.Data;
                    if (Product != null && Product.Images != null)
                    {
                        Product.Images = Product.Images
                            .OrderByDescending(i => i.IsPrimary) // ảnh chính lên đầu
                            .ToList();
                    }
                    if (Product == null) Product = new ProductDTO(); // Khởi tạo Product để tránh null reference
                    else
                    {
                        // Fetch provider email via profile endpoint
                        try
                        {
                            var profileRequestUri = $"api/profile/{Product.ProviderId}";
                            var profileResponse = await client.GetAsync(profileRequestUri);
                            if (profileResponse.IsSuccessStatusCode)
                            {
                                var raw = await profileResponse.Content.ReadAsStringAsync();
                                var doc = JsonSerializer.Deserialize<JsonElement>(raw, _jsonOptions);
                                if (doc.TryGetProperty("data", out var dataEl))
                                {
                                    if (dataEl.TryGetProperty("user", out var userEl) && userEl.TryGetProperty("email", out var emailEl))
                                    {
                                        ProviderEmail = emailEl.GetString();
                                    }
                                }
                            }
                        }
                        catch { }


                    }
                }
                else
                {
                    // Xử lý lỗi nếu không tải được sản phẩm
                    var errorContent = await productResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error fetching product. Status: {productResponse.StatusCode}. URI: {productRequestUri}. Content: {errorContent}");
                    Product = new ProductDTO(); // Khởi tạo Product rỗng để tránh null
                }

                // Fetch feedbacks with pagination using correct endpoint
                // Pass currentUserId to backend so it can filter blocked feedbacks properly
                var feedbacksRequestUri = $"api/feedbacks/product/{id}?page={CurrentPage}&pageSize={PageSize}";
                if (CurrentUserId.HasValue)
                {
                    feedbacksRequestUri += $"&currentUserId={CurrentUserId.Value}";
                }
                var feedbacksResponse = await client.GetAsync(feedbacksRequestUri);

                if (feedbacksResponse.IsSuccessStatusCode)
                {
                    try
                    {
                        var responseContent = await feedbacksResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"LoadInitialData - Feedback API Response: {responseContent}"); // Debug log
                        
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<PaginatedResponse<FeedbackResponseDto>>>(responseContent, _jsonOptions);
                        var paginatedData = apiResponse?.Data;
                        
                        if (paginatedData != null)
                        {
                            Feedbacks = paginatedData.Items ?? new List<FeedbackResponseDto>();
                            TotalFeedbacks = paginatedData.TotalItems;
                            VisibleFeedbacks = paginatedData.VisibleCount ?? paginatedData.TotalItems;
                            TotalPages = paginatedData.TotalPages;
                            // Don't override CurrentPage - keep the value from query parameter
                            Console.WriteLine($"API returned page: {paginatedData.Page}, keeping CurrentPage: {CurrentPage}");
                        }
                        else
                        {
                            Feedbacks = new List<FeedbackResponseDto>();
                            TotalFeedbacks = 0;
                            TotalPages = 1;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"LoadInitialData - JSON Error parsing feedbacks: {jsonEx.Message}");
                        Feedbacks = new List<FeedbackResponseDto>();
                        TotalFeedbacks = 0;
                        TotalPages = 1;
                    }

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

                // Check favorite status and if user owns this product
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                    {
                        // Check if current user is the provider of this product
                        if (Product != null && Product.ProviderId == userGuid)
                        {
                            IsOwnProduct = true;
                            Console.WriteLine($"IsOwnProduct set to TRUE - UserId: {userGuid}, ProviderId: {Product.ProviderId}");
                        }
                        else
                        {
                            Console.WriteLine($"IsOwnProduct is FALSE - UserId: {userGuid}, ProviderId: {Product?.ProviderId}");
                        }

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
                ErrorMessage = "An error occurred while loading product data.";
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString))
            {
                CurrentUserId = Guid.Parse(userIdString);
            }
        }


        public async Task<bool> CanFeedback(Guid id)
        {
            Guid orderItemId;
            AccessToken = _httpContextAccessor.HttpContext?.Request.Cookies["AccessToken"];




            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);
            var requestURI = $"{apiBaseUrl}/orders/order-item/{id}";
            var orderItemresponse = await client.GetAsync(requestURI);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };


            if (orderItemresponse.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<String>>(await orderItemresponse.Content.ReadAsStringAsync(), options);

                string resutl = apiResponse?.Data;

                if (resutl != null)
                {
                    orderItemId = Guid.Parse(resutl);
                    return true;
                }
                else
                {
                    orderItemId = Guid.Empty;
                    Console.WriteLine($"User need to rent it to use feedback function");
                    return false;
                }

            }
            else
            {
                var errorContent = await orderItemresponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Error fetching");
                return false;
            }
        }

        /// <summary>
        /// Sets the default transaction type based on product availability
        /// </summary>
        private void SetDefaultTransactionType()
        {
            if (Product == null) return;

            bool canRent = Product.RentalStatus == "Available" && Product.PricePerDay > 0;
            bool canPurchase = Product.PurchaseStatus == "Available" && Product.PurchasePrice > 0;

            // Set default based on availability
            if (canRent && !canPurchase)
            {
                // Only rental available
                TransactionType = BusinessObject.Enums.TransactionType.rental;
            }
            else if (!canRent && canPurchase)
            {
                // Only purchase available
                TransactionType = BusinessObject.Enums.TransactionType.purchase;
            }
            else if (canRent && canPurchase)
            {
                // Both available, default to rental
                TransactionType = BusinessObject.Enums.TransactionType.rental;
            }
            // If neither is available, keep the default (Rental) - the form will be disabled anyway
        }

        /// <summary>
        /// AJAX handler for loading reviews with pagination
        /// Called by JavaScript when user clicks pagination buttons
        /// </summary>
        public async Task<IActionResult> OnGetReviewsAsync(Guid id, [FromQuery] int page = 1)
        {
            // Validate page number
            CurrentPage = page > 0 ? page : 1;
            
            // Load product and feedback data
            await LoadInitialData(id, CurrentPage);
            
            // Return partial page (JavaScript will extract the reviews section)
            return Page();
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
