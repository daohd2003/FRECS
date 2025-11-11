using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.ProductDto; // Ensure this is available for Product image and price
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonStringEnumConverter

namespace ShareItFE.Pages.CartPage
{
    public class CartModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public CartModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public CartDto? Cart { get; set; }
        public decimal Subtotal { get; set; }
        
        /// <summary>
        /// Subtotal for rental items only
        /// </summary>
        public decimal RentalSubtotal { get; set; }
        
        /// <summary>
        /// Subtotal for purchase items only
        /// </summary>
        public decimal PurchaseSubtotal { get; set; }
        
        //public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }
        
        /// <summary>
        /// Total deposit amount for rental items
        /// </summary>
        public decimal TotalDeposit { get; set; }

        /// <summary>
        /// Check if cart has rental items
        /// </summary>
        public bool HasRentalItems => Cart?.Items?.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.rental) ?? false;

        /// <summary>
        /// Check if cart has purchase items
        /// </summary>
        public bool HasPurchaseItems => Cart?.Items?.Any(i => i.TransactionType == BusinessObject.Enums.TransactionType.purchase) ?? false;

        /// <summary>
        /// Selected discount code from session (to restore on page load)
        /// </summary>
        public DiscountCodeSessionDto? SelectedDiscountCode { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found.");
            }
            return Guid.Parse(userIdClaim);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var userId = GetUserId();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var response = await client.GetAsync($"api/cart");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<CartDto>>(
                        await response.Content.ReadAsStringAsync(), options);

                    Cart = apiResponse?.Data;

                    if (Cart != null && Cart.Items != null)
                    {
                        // Cập nhật tính toán Subtotal: Price * Days * Quantity
                        Subtotal = Cart.Items.Sum(item => item.TotalItemPrice);
                        
                        // Calculate separate subtotals for rental and purchase items
                        RentalSubtotal = Cart.Items
                            .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                            .Sum(item => item.TotalItemPrice);
                        
                        PurchaseSubtotal = Cart.Items
                            .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                            .Sum(item => item.TotalItemPrice);
                        
                        // Calculate total deposit for rental items
                        TotalDeposit = Cart.TotalDepositAmount;
                        
                        // Load discount code from session (if exists)
                        LoadDiscountFromSession();
                        
                        //DeliveryFee = Subtotal > 100000 ? 0 : 15000; // Ví dụ: miễn phí giao hàng nếu tổng tiền > 100
                        //Total = Subtotal + DeliveryFee + TotalDeposit;
                        Total = Subtotal + TotalDeposit;
                    }
                    else
                    {
                        Cart = new CartDto { CustomerId = userId, Items = new List<CartItemDto>(), TotalAmount = 0, TotalDepositAmount = 0 };
                        //Subtotal = 0; DeliveryFee = 0; Total = 0 + 0;
                        Subtotal = 0; 
                        RentalSubtotal = 0; 
                        PurchaseSubtotal = 0; 
                        TotalDeposit = 0; 
                        Total = Subtotal + TotalDeposit;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Could not load cart: {errorContent}";
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToPage("/Auth");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            }
            return Page();
        }

        // --- NEW: Page Handler để cập nhật QUANTITY ---
        public async Task<IActionResult> OnPostUpdateQuantityAsync(Guid itemId, int currentQuantity, string action)
        {
            if (itemId == Guid.Empty)
            {
                ErrorMessage = "Invalid product ID.";
                return RedirectToPage();
            }

            int newQuantity = currentQuantity;
            if (action == "increase")
            {
                newQuantity = currentQuantity + 1;
            }
            else if (action == "decrease")
            {
                newQuantity = currentQuantity - 1;
                if (newQuantity < 1) // Nếu Quantity về 0 hoặc nhỏ hơn, xóa item
                {
                    return await OnPostRemoveFromCartAsync(itemId);
                }
            }
            else
            {
                ErrorMessage = "Invalid quantity update action.";
                return RedirectToPage();
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var updateDto = new CartUpdateRequestDto
            {
                Quantity = newQuantity, // Chỉ gửi Quantity
                RentalDays = null // Đảm bảo RentalDays là null để không cập nhật
            };

            var response = await client.PutAsJsonAsync($"api/cart/{itemId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                return new JsonResult(new { success = true, message = "Product quantity updated successfully." });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    // Try to parse error response to extract clean message
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return new JsonResult(new { success = false, message = errorResponse?.Message ?? errorContent }) 
                    { 
                        StatusCode = (int)response.StatusCode 
                    };
                }
                catch
                {
                    // If parsing fails, use raw content
                    return new JsonResult(new { success = false, message = errorContent }) 
                    { 
                        StatusCode = (int)response.StatusCode 
                    };
                }
            }
        }

        // --- RENAMED: Page Handler để cập nhật RENTAL DAYS ---
        public async Task<IActionResult> OnPostUpdateRentalDaysAsync(Guid itemId, int currentRentalDays, string action) // Đổi tên handler
        {
            if (itemId == Guid.Empty)
            {
                ErrorMessage = "Invalid product ID.";
                return RedirectToPage();
            }

            int newRentalDays = currentRentalDays;
            if (action == "increase")
            {
                newRentalDays = currentRentalDays + 1;
                if (newRentalDays > 30)
                {
                    newRentalDays = 30; // Cap at maximum 30 days
                    ErrorMessage = "Rental days cannot exceed 30 days.";
                    return RedirectToPage();
                }
            }
            else if (action == "decrease")
            {
                newRentalDays = currentRentalDays - 1;
                if (newRentalDays < 1)
                {
                    // Nếu số ngày thuê về 0 hoặc nhỏ hơn, có thể đặt lại là 1 hoặc xóa item
                    newRentalDays = 1; // Đặt lại về 1 ngày tối thiểu
                    ErrorMessage = "Rental days must be at least 1.";
                }
            }
            else
            {
                ErrorMessage = "Invalid rental days update action.";
                return RedirectToPage();
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var updateDto = new CartUpdateRequestDto
            {
                RentalDays = newRentalDays, // Chỉ gửi RentalDays
                Quantity = null // Đảm bảo Quantity là null để không cập nhật
            };

            var response = await client.PutAsJsonAsync($"api/cart/{itemId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Product rental days in cart have been updated successfully.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    // Try to parse error response to extract clean message
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = errorResponse?.Message ?? errorContent;
                }
                catch
                {
                    // If parsing fails, use raw content
                    ErrorMessage = errorContent;
                }
            }
            return RedirectToPage();
        }

        // NEW: Global schedule update (apply StartDate and/or RentalDays to entire cart)
        public async Task<IActionResult> OnPostUpdateCartScheduleAsync(Guid itemId, DateTime? startDate, int? rentalDays, string? action)
        {
            // Use the existing API signature by passing one item's id; backend will apply to all
            var client = await _clientHelper.GetAuthenticatedClientAsync();

            // Adjust rentalDays based on action buttons, if provided
            if (!rentalDays.HasValue && action is "increaseDays" or "decreaseDays")
            {
                // If not provided, we cannot infer current; just ignore here and rely on explicit input path
                action = "apply";
            }
            else if (rentalDays.HasValue && action is "increaseDays")
            {
                rentalDays = Math.Min(30, Math.Max(1, rentalDays.Value + 1));
            }
            else if (rentalDays.HasValue && action is "decreaseDays")
            {
                rentalDays = Math.Max(1, rentalDays.Value - 1);
            }
            
            // Validate rental days limit
            if (rentalDays.HasValue && rentalDays.Value > 30)
            {
                ErrorMessage = "Rental days cannot exceed 30 days.";
                return RedirectToPage();
            }

            var updateDto = new CartUpdateRequestDto
            {
                StartDate = startDate,
                RentalDays = rentalDays
            };

            var response = await client.PutAsJsonAsync($"api/cart/{itemId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Applied rental schedule to all items in the cart.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    // Try to parse error response to extract clean message
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = errorResponse?.Message ?? errorContent;
                }
                catch
                {
                    // If parsing fails, use raw content
                    ErrorMessage = errorContent;
                }
            }

            return RedirectToPage();
        }
        // --- NEW: Page Handler để cập nhật START DATE ---
        public async Task<IActionResult> OnPostUpdateStartDateAsync(Guid itemId, DateTime startDate)
        {
            if (itemId == Guid.Empty)
            {
                ErrorMessage = "Invalid product ID.";
                return RedirectToPage();
            }

            if (startDate.Date < DateTime.UtcNow.Date)
            {
                ErrorMessage = "Start date cannot be in the past.";
                return RedirectToPage();
            }

            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var updateDto = new CartUpdateRequestDto
            {
                StartDate = startDate
            };

            var response = await client.PutAsJsonAsync($"api/cart/{itemId}", updateDto);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Rental start date updated.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    // Try to parse error response to extract clean message
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = errorResponse?.Message ?? errorContent;
                }
                catch
                {
                    // If parsing fails, use raw content
                    ErrorMessage = errorContent;
                }
            }

            return RedirectToPage();
        }
        // --- EXISTING: Page Handler để xóa Item ---
        public async Task<IActionResult> OnPostRemoveFromCartAsync(Guid itemId)
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var response = await client.DeleteAsync($"api/cart/{itemId}");

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Product has been removed from cart successfully.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Could not remove product: {errorContent}";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetAvailableDiscountCodesAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("api/DiscountCode/user-available");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new { message = "Failed to load discount codes" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Save selected discount code to session
        /// </summary>
        public IActionResult OnPostSaveDiscountCodeAsync([FromBody] DiscountCodeSessionDto discountCode)
        {
            try
            {
                if (discountCode == null)
                {
                    return BadRequest(new { message = "Invalid discount code data" });
                }

                // Save to session as JSON
                HttpContext.Session.SetString("SelectedDiscountCode", JsonSerializer.Serialize(discountCode));
                
                return new JsonResult(new { success = true, message = "Discount code saved to session" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Remove discount code from session
        /// </summary>
        public IActionResult OnPostRemoveDiscountCodeAsync()
        {
            try
            {
                HttpContext.Session.Remove("SelectedDiscountCode");
                return new JsonResult(new { success = true, message = "Discount code removed from session" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Load discount code from session (to restore on page load)
        /// </summary>
        private async void LoadDiscountFromSession()
        {
            try
            {
                var discountJson = HttpContext.Session.GetString("SelectedDiscountCode");
                
                if (!string.IsNullOrEmpty(discountJson))
                {
                    var sessionDiscount = JsonSerializer.Deserialize<DiscountCodeSessionDto>(discountJson);
                    
                    if (sessionDiscount != null && Guid.TryParse(sessionDiscount.Id, out var discountId))
                    {
                        // Check if this discount code is still available for the user
                        var client = await _clientHelper.GetAuthenticatedClientAsync();
                        var response = await client.GetAsync("api/DiscountCode/user-available");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var apiResponse = JsonSerializer.Deserialize<ApiResponse<IEnumerable<DiscountCodeSessionDto>>>(
                                content, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            
                            var availableCodes = apiResponse?.Data ?? Enumerable.Empty<DiscountCodeSessionDto>();
                            
                            // Check if the session discount code is still in the available list
                            var isStillAvailable = availableCodes.Any(dc => dc.Id == sessionDiscount.Id);
                            
                            if (isStillAvailable)
                            {
                                // Discount is still available, keep it
                                SelectedDiscountCode = sessionDiscount;
                            }
                            else
                            {
                                // Discount has been used or is no longer available, clear session
                                HttpContext.Session.Remove("SelectedDiscountCode");
                                SelectedDiscountCode = null;
                            }
                        }
                        else
                        {
                            // If API call fails, keep the session discount (benefit of the doubt)
                            SelectedDiscountCode = sessionDiscount;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SelectedDiscountCode = null;
            }
        }
    }

    /// <summary>
    /// DTO for storing discount code in session
    /// </summary>
    public class DiscountCodeSessionDto
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string DiscountType { get; set; }
        public decimal Value { get; set; }
        public string UsageType { get; set; }
    }
}