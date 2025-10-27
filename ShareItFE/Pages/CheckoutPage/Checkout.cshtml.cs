using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.ProfileDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.DTOs.VNPay;
using ShareItFE.Extensions;
using System.ComponentModel.DataAnnotations;

namespace ShareItFE.Pages.CheckoutPage
{
    public class CheckoutModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public string frontendBaseUrl { get; set; }
        public string backendBaseUrl { get; set; }
        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);
        public CheckoutModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
            frontendBaseUrl = _configuration.GetFrontendBaseUrl(_environment);
            backendBaseUrl = _configuration.GetApiRootUrl(_environment);
        }

        [BindProperty]
        public CheckoutInputModel Input { get; set; } = new CheckoutInputModel();

        public CartDto? Cart { get; set; }
        public OrderDetailsDto? SingleOrder { get; set; }
        public decimal Subtotal { get; set; }
        
        /// <summary>
        /// Subtotal for rental items only
        /// </summary>
        public decimal RentalSubtotal { get; set; }
        
        /// <summary>
        /// Subtotal for purchase items only
        /// </summary>
        public decimal PurchaseSubtotal { get; set; }
        
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }
        
        /// <summary>
        /// Total deposit amount for rental items
        /// </summary>
        public decimal TotalDeposit { get; set; }

        /// <summary>
        /// Discount amount calculated from session discount code
        /// </summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Selected discount code from session
        /// </summary>
        public DiscountCodeSessionInfo? SelectedDiscountCode { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string QrCodeUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? OrderId { get; set; }
        
        /// <summary>
        /// Store OrderId in TempData to persist across page navigation (e.g., back to cart)
        /// </summary>
        [TempData]
        public Guid? PendingOrderId { get; set; }
        
        // Helper methods to work with Session directly
        private string GetPendingCartOrderIds()
        {
            return HttpContext.Session.GetString("PendingCartOrderIds");
        }

        private void SetPendingCartOrderIds(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                HttpContext.Session.Remove("PendingCartOrderIds");
            }
            else
            {
                HttpContext.Session.SetString("PendingCartOrderIds", value);
            }
        }

        /// <summary>
        /// Indicates whether the user's profile has all required information (PhoneNumber and Address)
        /// </summary>
        [TempData]
        public bool HasRequiredProfileInfo { get; set; } = false;

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
            // Note: We'll save OrderId to session AFTER validating it's a pending order
            // to prevent accidentally storing completed/returned orders
            
            // Only clear TempData if this is a fresh GET request (not after POST with QR code)
            // Check TempData to see if we just created a transaction
            var hasNewTransaction = TempData.Peek("TransactionId") != null;
            bool isAfterQRCreation = hasNewTransaction && !string.IsNullOrEmpty(SuccessMessage);
            
            if (!hasNewTransaction && string.IsNullOrEmpty(SuccessMessage))
            {
                // This is a fresh GET request, clear old data
                QrCodeUrl = null;
                TempData["TransactionId"] = null;
            }
            
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var userId = GetUserId();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                // KIỂM TRA ĐẦU TIÊN: Nếu có OrderId từ "Rent Again", ưu tiên tải đơn hàng đó
                if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                {
                    // Call the API to get details of the specific order
                    // Ensure your API endpoint for OrderDetailsDto is correct, e.g., api/orders/{id}/details
                    var orderResponse = await client.GetAsync($"{backendBaseUrl}/api/orders/{OrderId.Value}/details");

                    if (orderResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDetailsDto>>(
                            await orderResponse.Content.ReadAsStringAsync(), options);
                        SingleOrder = apiResponse?.Data;

                        if (SingleOrder == null || SingleOrder.Id == Guid.Empty) // Kiểm tra thêm SingleOrder có hợp lệ không
                        {
                            ErrorMessage = $"Order details not found for ID: {OrderId.Value} or order is invalid.";
                            // Nếu không tìm thấy đơn hàng cụ thể, chuyển hướng về trang Profile Orders để tránh kẹt
                            return RedirectToPage("/Profile", new { tab = "orders" });
                        }

                        // Kiểm tra trạng thái đơn hàng: Chỉ cho phép thanh toán đơn pending
                        if (SingleOrder.Status != BusinessObject.Enums.OrderStatus.pending)
                        {
                            ErrorMessage = $"Order {SingleOrder.OrderCode} is not in pending payment status. Current status is: {SingleOrder.Status.ToString().Replace("_", " ")}.";
                            return RedirectToPage("/Profile", new { tab = "orders" });
                        }

                        // ✅ NOW save to session AFTER validating order is pending
                        // This prevents storing completed/returned orders that shouldn't be cancelled
                        PendingOrderId = OrderId.Value;
                        var orderIdsList = new List<Guid> { OrderId.Value };
                        var orderIdsJson = JsonSerializer.Serialize(orderIdsList);
                        SetPendingCartOrderIds(orderIdsJson);

                        // Get totals from the single order (đã được tính đúng trong database)
                        Subtotal = SingleOrder.Subtotal; // Chỉ giá thuê/mua
                        TotalDeposit = SingleOrder.TotalDepositAmount; // Chỉ tiền cọc
                        DiscountAmount = SingleOrder.DiscountAmount; // Số tiền giảm giá từ order
                        Total = SingleOrder.TotalAmount; // Tổng = subtotal + deposit - discount
                        
                        // Calculate separate subtotals for rental and purchase items (needed for discount display)
                        RentalSubtotal = SingleOrder.Items
                            .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                            .Sum(item => item.PricePerDay * (item.RentalDays ?? 0) * item.Quantity);
                        
                        PurchaseSubtotal = SingleOrder.Items
                            .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                            .Sum(item => item.PricePerDay * item.Quantity);
                        
                        // If order has discount code, fetch details and restore to session
                        if (SingleOrder.DiscountCodeId.HasValue && SingleOrder.DiscountCodeId.Value != Guid.Empty)
                        {
                            await RestoreDiscountFromOrder(SingleOrder.DiscountCodeId.Value, client);
                        }
                        
                        // Populate Input fields with existing shipping address from the order
                        if (SingleOrder.ShippingAddress != null)
                        {
                            Input.CustomerFullName = SingleOrder.ShippingAddress.FullName;
                            Input.Email = SingleOrder.ShippingAddress.Email;
                            Input.PhoneNumber = SingleOrder.ShippingAddress.Phone;
                            Input.Address = SingleOrder.ShippingAddress.Address;
                            // Don't use profile when loading from existing order
                            Input.UseSameProfile = false;
                        }
                        
                        // Load profile to check if required info is available (for validation)
                        var profileResponse = await client.GetAsync("api/profile/my-profile-for-checkout");
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var profileApiResponse = JsonSerializer.Deserialize<ApiResponse<ProfileDetailDto>>(
                                await profileResponse.Content.ReadAsStringAsync(), options);
                            var profile = profileApiResponse?.Data;

                            if (profile != null)
                            {
                                // Check if profile has required information (PhoneNumber and Address)
                                HasRequiredProfileInfo = !string.IsNullOrWhiteSpace(profile.PhoneNumber) && 
                                                        !string.IsNullOrWhiteSpace(profile.Address);
                            }
                            else
                            {
                                HasRequiredProfileInfo = false;
                            }
                        }
                        else
                        {
                            HasRequiredProfileInfo = false;
                        }
                        
                        Cart = null;

                    }
                    else
                    {
                        var errorContent = await orderResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Could not load order information {OrderId.Value}: {orderResponse.StatusCode} - {errorContent}";
                        return RedirectToPage("/Profile", new { tab = "orders" });
                    }
                }
                else
                { // Lấy thông tin giỏ hàng
                    var cartResponse = await client.GetAsync("api/cart");
                    if (cartResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CartDto>>(
                            await cartResponse.Content.ReadAsStringAsync(), options);
                        Cart = apiResponse?.Data;

                        if (Cart == null || !Cart.Items.Any())
                        {
                            ErrorMessage = "Your cart is empty. Please add products before checkout.";
                            return RedirectToPage("/CartPage/Cart");
                        }
                        else
                        {
                            Subtotal = Cart.Items.Sum(item => item.TotalItemPrice);
                            
                            // Calculate separate subtotals for rental and purchase items
                            RentalSubtotal = Cart.Items
                                .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                                .Sum(item => item.TotalItemPrice);
                            
                            PurchaseSubtotal = Cart.Items
                                .Where(item => item.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                                .Sum(item => item.TotalItemPrice);
                            
                            // Calculate total deposit for rental items from cart
                            TotalDeposit = Cart.TotalDepositAmount;
                            
                            // Load discount code from session and calculate discount
                            LoadDiscountFromSession();
                            
                            // Giả định logic tính phí giao hàng và thuế
                            //DeliveryFee = Subtotal > 100000 ? 0 : 15000;
                            //Total = Subtotal + DeliveryFee + TotalDeposit;
                            Total = Subtotal + TotalDeposit - DiscountAmount;
                        }
                    }
                    else
                    {
                        var errorContent = await cartResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Could not load cart: {errorContent}";
                        return RedirectToPage("/CartPage/Cart");
                    }

                    // Lấy thông tin profile cho trang thanh toán
                    // Skip profile check if we're returning after QR code creation (to preserve HasRequiredProfileInfo)
                    if (!isAfterQRCreation && string.IsNullOrEmpty(Input.CustomerFullName) && Input.UseSameProfile)
                    {
                        var profileResponse = await client.GetAsync("api/profile/my-profile-for-checkout");
                        // Thêm logging để kiểm tra
                        Console.WriteLine($"Status Code: {profileResponse.StatusCode}");
                        Console.WriteLine($"Response Content: {await profileResponse.Content.ReadAsStringAsync()}");
                        Console.WriteLine($"Request Headers: {client.DefaultRequestHeaders}");
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var profileApiResponse = JsonSerializer.Deserialize<ApiResponse<ProfileDetailDto>>(
                                await profileResponse.Content.ReadAsStringAsync(), options);
                            var profile = profileApiResponse?.Data;

                            if (profile != null)
                            {
                                Input.CustomerFullName = profile.FullName;
                                Input.Email = profile.Email;
                                Input.PhoneNumber = profile.PhoneNumber;
                                Input.Address = profile.Address;
                                
                                // Check if profile has required information (PhoneNumber and Address)
                                HasRequiredProfileInfo = !string.IsNullOrWhiteSpace(profile.PhoneNumber) && 
                                                        !string.IsNullOrWhiteSpace(profile.Address);
                                
                                // If missing required info, uncheck the checkbox
                                if (!HasRequiredProfileInfo)
                                {
                                    Input.UseSameProfile = false;
                                }
                                else
                                {
                                    Input.UseSameProfile = true;
                                }
                            }
                            else
                            {
                                HasRequiredProfileInfo = false;
                                Input.UseSameProfile = false;
                            }
                        }
                        else if (profileResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            Console.WriteLine("No profile found for this user. Manual input required.");
                            // Nếu không tìm thấy profile, mặc định bỏ chọn "UseSameProfile"
                            HasRequiredProfileInfo = false;
                            Input.UseSameProfile = false;
                        }
                        else
                        {
                            var errorContent = await profileResponse.Content.ReadAsStringAsync();
                            ErrorMessage = $"Could not load user profile: {errorContent}";
                            // Mặc định bỏ chọn "UseSameProfile" nếu có lỗi khi tải profile
                            HasRequiredProfileInfo = false;
                            Input.UseSameProfile = false;
                        }
                    }
                    else if (!isAfterQRCreation)
                    {
                        // If CustomerFullName is already populated, check if we have required info
                        // But skip if this is after QR creation
                        HasRequiredProfileInfo = !string.IsNullOrWhiteSpace(Input.PhoneNumber) && 
                                                !string.IsNullOrWhiteSpace(Input.Address);
                    }
                    // If isAfterQRCreation = true, HasRequiredProfileInfo is already preserved from TempData
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

        public IActionResult OnPostClearPendingCartOrders()
        {
            // Clear pending cart order IDs from session when payment is successful
            SetPendingCartOrderIds(null);
            return new JsonResult(new { success = true });
        }

        /// <summary>
        /// Clear PendingCartOrderIds session - used when starting a new flow like "Rent Again"
        /// </summary>
        [HttpPost]
        public IActionResult OnPostClearPendingSession()
        {
            SetPendingCartOrderIds(null);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostClearSession(string? transactionId)
        {
            // Preserve important data before clearing TempData
            var preservedPendingOrderId = PendingOrderId;
            // Note: PendingCartOrderIds is now in Session, not TempData, so it's automatically preserved
            
            // Clear TempData and messages when user closes QR modal
            TempData.Clear();
            SuccessMessage = null;
            ErrorMessage = null;
            QrCodeUrl = null;
            
            // Restore preserved data after clearing TempData
            if (preservedPendingOrderId.HasValue)
            {
                PendingOrderId = preservedPendingOrderId;
            }
            
            // Send notification if transactionId is provided
            if (!string.IsNullOrEmpty(transactionId) && Guid.TryParse(transactionId, out var txnId))
            {
                try
                {
                    var client = await _clientHelper.GetAuthenticatedClientAsync();
                    var userId = GetUserId();
                    
                    // Call API to send transaction failed notification
                    var notifyUrl = $"{backendBaseUrl}/api/notification/transaction-failed?transactionId={txnId}&userId={userId}";
                    await client.PostAsync(notifyUrl, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending notification: {ex.Message}");
                    // Don't fail the request if notification fails
                }
            }
            
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Policy agreement is handled on Cart page - preserve the value from form submission
            
            // Remove validation errors for PhoneNumber and Address if UseSameProfile is true
            // because these will be loaded from profile
            if (Input.UseSameProfile)
            {
                ModelState.Remove("Input.PhoneNumber");
                ModelState.Remove("Input.Address");
            }

            if (!ModelState.IsValid)
            {
                var currentInputState = Input;
                await OnGetAsync();
                Input.CustomerFullName = currentInputState.CustomerFullName;
                Input.Email = currentInputState.Email;
                Input.PhoneNumber = currentInputState.PhoneNumber;
                Input.Address = currentInputState.Address;
                Input.PaymentMethod = currentInputState.PaymentMethod;
                Input.UseSameProfile = currentInputState.UseSameProfile;
                return Page();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                // Reload profile info if "UseSameProfile" is checked
                if (Input.UseSameProfile)
                {
                    var profileResponse = await client.GetAsync($"{backendBaseUrl}/api/profile/my-profile-for-checkout");
                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var profileApiResponse = JsonSerializer.Deserialize<ApiResponse<ProfileDetailDto>>(
                            await profileResponse.Content.ReadAsStringAsync(), options);
                        var profile = profileApiResponse?.Data;

                        if (profile != null)
                        {
                            Input.CustomerFullName = string.IsNullOrEmpty(Input.CustomerFullName) ? profile.FullName : Input.CustomerFullName;
                            Input.Email = string.IsNullOrEmpty(Input.Email) ? profile.Email : Input.Email;
                            Input.PhoneNumber = string.IsNullOrEmpty(Input.PhoneNumber) ? profile.PhoneNumber : Input.PhoneNumber;
                            Input.Address = string.IsNullOrEmpty(Input.Address) ? profile.Address : Input.Address;
                        }
                    }
                }

                List<Guid> orderIdsToProcess = new List<Guid>();
                decimal totalAmountForPayment = 0;

                // Determine the actual order ID to process (from query string or TempData)
                var actualOrderId = OrderId ?? PendingOrderId;

                // SỬA: Logic phân biệt giữa "Rent Again" (OrderId đã có) và "Cart Checkout"
                if (actualOrderId.HasValue && actualOrderId.Value != Guid.Empty)
                {
                    // Case 1: Processing a single order from "Pay Now" or "Rent Again"
                    // IMPORTANT: Clear cart first to prevent creating duplicate orders
                    try
                    {
                        var clearCartResponse = await client.DeleteAsync($"{backendBaseUrl}/api/cart/clear");
                        if (clearCartResponse.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Cart cleared successfully before processing order payment.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not clear cart: {ex.Message}");
                        // Continue anyway, clearing cart is not critical
                    }
                    
                    orderIdsToProcess.Add(actualOrderId.Value);

                    // Fetch the single order details again to get the accurate TotalAmount for payment
                    // (This ensures we have the latest total and status from the API)
                    var orderResponse = await client.GetAsync($"{backendBaseUrl}/api/orders/{actualOrderId.Value}/details");
                    if (orderResponse.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDetailsDto>>(
                            await orderResponse.Content.ReadAsStringAsync(), options);
                        SingleOrder = apiResponse?.Data;

                        if (SingleOrder == null || SingleOrder.Status != BusinessObject.Enums.OrderStatus.pending)
                        {
                            ErrorMessage = "Invalid order or order not in pending payment status.";
                            return RedirectToPage("/Profile", new { tab = "orders" });
                        }
                        totalAmountForPayment = SingleOrder.TotalAmount; // Use the total from the fetched order

                        var updateContactInfoRequest = new UpdateOrderContactInfoDto
                        {
                            OrderId = actualOrderId.Value,
                            CustomerFullName = Input.CustomerFullName ?? "",
                            CustomerEmail = Input.Email ?? "",
                            CustomerPhoneNumber = Input.PhoneNumber ?? "",
                            DeliveryAddress = Input.Address ?? "",
                            HasAgreedToPolicies = true
                        };

                        var updateInfoResponse = await client.PutAsJsonAsync($"{backendBaseUrl}/api/orders/update-contact-info", updateContactInfoRequest);

                        if (!updateInfoResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await updateInfoResponse.Content.ReadAsStringAsync();
                            var apiErrorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, options);
                            ErrorMessage = apiErrorResponse?.Message ?? $"Could not update contact information for order: {updateInfoResponse.StatusCode}.";
                            Input = new CheckoutInputModel();
                            await OnGetAsync();
                            return Page();
                        }
                    }
                    else
                    {
                        var errorContent = await orderResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Could not load order information for payment: {orderResponse.StatusCode} - {errorContent}";
                        return RedirectToPage("/Profile", new { tab = "orders" });
                    }
                }
                else // Case 2: Processing a normal cart checkout
                {
                    // Check if there are pending orders from previous cart checkout
                    // If yes, delete them before creating new orders
                    var pendingCartOrderIds = GetPendingCartOrderIds();
                    
                    if (!string.IsNullOrEmpty(pendingCartOrderIds))
                    {
                        try
                        {
                            var pendingOrderIdList = JsonSerializer.Deserialize<List<Guid>>(pendingCartOrderIds);
                            
                            if (pendingOrderIdList != null && pendingOrderIdList.Any())
                            {
                                foreach (var pendingOrderId in pendingOrderIdList)
                                {
                                    try
                                    {
                                        // Safety check: Verify order status before cancelling
                                        var orderCheckResponse = await client.GetAsync($"{backendBaseUrl}/api/orders/{pendingOrderId}/details");
                                        
                                        if (orderCheckResponse.IsSuccessStatusCode)
                                        {
                                            var orderCheckContent = await orderCheckResponse.Content.ReadAsStringAsync();
                                            var orderCheckApiResponse = JsonSerializer.Deserialize<ApiResponse<OrderDetailsDto>>(
                                                orderCheckContent, 
                                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                            
                                            var orderToCheck = orderCheckApiResponse?.Data;
                                            
                                            // Only cancel if order is truly pending
                                            if (orderToCheck != null && orderToCheck.Status == BusinessObject.Enums.OrderStatus.pending)
                                            {
                                                // Step 1: Cancel pending order (must cancel before delete)
                                                var cancelResponse = await client.PutAsync($"{backendBaseUrl}/api/orders/{pendingOrderId}/cancel", null);
                                                
                                                if (cancelResponse.IsSuccessStatusCode)
                                                {
                                                    // Step 2: Delete the cancelled order permanently
                                                    var deleteResponse = await client.DeleteAsync($"{backendBaseUrl}/api/orders/{pendingOrderId}");
                                                    
                                                    if (!deleteResponse.IsSuccessStatusCode)
                                                    {
                                                        var deleteError = await deleteResponse.Content.ReadAsStringAsync();
                                                        Console.WriteLine($"[WARNING] Could not delete order {pendingOrderId}: {deleteError}");
                                                    }
                                                }
                                                else
                                                {
                                                    var errorContent = await cancelResponse.Content.ReadAsStringAsync();
                                                    Console.WriteLine($"[WARNING] Could not cancel pending order {pendingOrderId}: {errorContent}");
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"[INFO] Skipping order {pendingOrderId} - not pending status (status: {orderToCheck?.Status})");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[WARNING] Could not fetch order {pendingOrderId} for status check");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[ERROR] Failed to process pending order {pendingOrderId}: {ex.Message}");
                                        // Continue with other orders even if one fails
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Could not parse pending order IDs: {ex.Message}");
                        }
                    }
                    
                    // Clear pending order IDs from session after processing
                    SetPendingCartOrderIds(null);
                    PendingOrderId = null;
                    
                    // Load discount from session before creating checkout request
                    LoadDiscountFromSession();
                    
                    // This section will create orders from the cart.
                    // This is where the call to api/cart/checkout is necessary.
                    var checkoutRequest = new CheckoutRequestDto
                    {
                        UseSameProfile = Input.UseSameProfile,
                        CustomerFullName = Input.CustomerFullName,
                        CustomerEmail = Input.Email,
                        CustomerPhoneNumber = Input.PhoneNumber,
                        DeliveryAddress = Input.Address,
                        HasAgreedToPolicies = true,
                        DiscountCodeId = SelectedDiscountCode != null && Guid.TryParse(SelectedDiscountCode.Id, out var discountId) 
                            ? discountId 
                            : null
                    };

                    var cartCheckoutResponse = await client.PostAsJsonAsync($"{backendBaseUrl}/api/cart/checkout", checkoutRequest);

                    if (!cartCheckoutResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await cartCheckoutResponse.Content.ReadAsStringAsync();
                        var apiErrorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        ErrorMessage = $"Could not create orders from cart: {apiErrorResponse.Message}";
                        Input = new CheckoutInputModel();
                        await OnGetAsync();
                        return Page();
                    }

                    var cartCheckoutApiResponse = JsonSerializer.Deserialize<ApiResponse<IEnumerable<OrderDto>>>(
                         await cartCheckoutResponse.Content.ReadAsStringAsync(), options);
                    var createdOrdersFromCart = cartCheckoutApiResponse?.Data?.ToList();

                    if (createdOrdersFromCart == null || !createdOrdersFromCart.Any())
                    {
                        ErrorMessage = "No orders were created from the cart.";
                        await OnGetAsync();
                        return Page();
                    }
                    orderIdsToProcess.AddRange(createdOrdersFromCart.Select(o => o.Id));
                    totalAmountForPayment = createdOrdersFromCart.Sum(o => o.TotalAmount);
                    
                    // Save new pending cart order IDs for future cleanup if needed
                    var orderIdsJson = JsonSerializer.Serialize(orderIdsToProcess);
                    SetPendingCartOrderIds(orderIdsJson);
                }

                // Proceed with payment for the order(s) identified in orderIdsToProcess
                if (!orderIdsToProcess.Any())
                {
                    ErrorMessage = "No orders to process payment for.";

                    Input = new CheckoutInputModel();
                    await OnGetAsync();
                    return Page();
                }

                Total = totalAmountForPayment; // Set the Total property for the page display (if it reloads)

                if (Input.PaymentMethod == "vnpay")
                {
                    var vnpayRequest = new CreatePaymentRequestDto
                    {
                        OrderIds = orderIdsToProcess,
                        Note = $"Payment for orders: {string.Join(", ", orderIdsToProcess.Select(id => id.ToString().Substring(0, 8)))}"
                    };

                    var vnpayResponse = await client.PostAsJsonAsync($"{backendBaseUrl}/api/payment/Vnpay/CreatePaymentUrl", vnpayRequest);

                    if (vnpayResponse.IsSuccessStatusCode)
                    {
                        var vnpayApiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(
                            await vnpayResponse.Content.ReadAsStringAsync(), options);
                        var paymentUrl = vnpayApiResponse?.Data;

                        if (!string.IsNullOrEmpty(paymentUrl))
                        {
                            // Preserve PendingOrderId before clearing TempData
                            var preservedPendingOrderId = PendingOrderId;
                            
                            // Clear any messages before redirecting to VNPay
                            // (User won't see them anyway, and they shouldn't appear when user returns)
                            SuccessMessage = null;
                            ErrorMessage = null;
                            TempData.Clear();
                            
                            // Restore PendingOrderId after clearing TempData
                            if (preservedPendingOrderId.HasValue)
                            {
                                PendingOrderId = preservedPendingOrderId;
                            }
                            
                            return Redirect(paymentUrl);
                        }
                        else
                        {
                            ErrorMessage = "Could not get VNPay payment URL.";
                        }
                    }
                    else
                    {
                        var errorContent = await vnpayResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Could not initialize VNPay payment: {errorContent}";
                    }
                }
                else if (Input.PaymentMethod == "qr")
                {
                    var createTransactionRequest = new CreateTransactionRequest { OrderIds = orderIdsToProcess };

                    var qrResponse = await client.PostAsJsonAsync($"{backendBaseUrl}/api/transactions/create", createTransactionRequest);

                    if (qrResponse.IsSuccessStatusCode)
                    {
                        var qrApiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
                            await qrResponse.Content.ReadAsStringAsync(), options);

                        if (qrApiResponse != null && qrApiResponse.Data.ValueKind != JsonValueKind.Null)
                        {
                            QrCodeUrl = qrApiResponse.Data.GetProperty("qrImageUrl").GetString();
                            var transactionIdFromApi = qrApiResponse.Data.GetProperty("transactionId").GetString();
                            TempData["TransactionId"] = transactionIdFromApi;
                            SuccessMessage = "QR code created successfully. Please scan to complete payment.";

                            // Save current input values before resetting
                            var currentInput = Input;
                            var currentHasRequiredInfo = HasRequiredProfileInfo;
                            
                            Input = new CheckoutInputModel
                            {
                                CustomerFullName = currentInput.CustomerFullName,
                                Email = currentInput.Email,
                                PhoneNumber = currentInput.PhoneNumber,
                                Address = currentInput.Address,
                                PaymentMethod = currentInput.PaymentMethod,
                                // UseSameProfile should be false if profile is incomplete
                                UseSameProfile = currentHasRequiredInfo
                            };
                            
                            // Preserve HasRequiredProfileInfo state
                            HasRequiredProfileInfo = currentHasRequiredInfo;
                            
                            // Note: PendingCartOrderIds is now in Session, automatically persists
                            
                            await OnGetAsync();
                            return Page();
                        }
                        else
                        {
                            ErrorMessage = "Could not get QR code data from API response.";
                        }
                    }
                    else
                    {
                        var errorContent = await qrResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Failed to create QR code: {errorContent}";
                    }
                }
                else
                {
                    ErrorMessage = "Invalid payment method selected.";
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

            // If any error occurred that didn't result in a redirect, reload the page
            await OnGetAsync();
            return Page();
        }

        /// <summary>
        /// Load discount code from session and calculate discount amount
        /// </summary>
        private void LoadDiscountFromSession()
        {
            try
            {
                var discountJson = HttpContext.Session.GetString("SelectedDiscountCode");
                
                if (!string.IsNullOrEmpty(discountJson))
                {
                    var discount = JsonSerializer.Deserialize<DiscountCodeSessionInfo>(discountJson);
                    
                    if (discount != null)
                    {
                        SelectedDiscountCode = discount;
                        
                        // Determine which subtotal to apply discount to based on UsageType
                        decimal applicableSubtotal = 0;
                        
                        if (discount.UsageType == "Rental")
                        {
                            applicableSubtotal = RentalSubtotal;
                        }
                        else if (discount.UsageType == "Purchase")
                        {
                            applicableSubtotal = PurchaseSubtotal;
                        }
                        
                        // Calculate discount amount
                        if (discount.DiscountType == "Percentage")
                        {
                            DiscountAmount = Math.Round(applicableSubtotal * (discount.Value / 100));
                        }
                        else // Fixed amount
                        {
                            DiscountAmount = discount.Value;
                        }
                        
                        // Ensure discount doesn't exceed applicable subtotal
                        DiscountAmount = Math.Min(DiscountAmount, applicableSubtotal);
                    }
                }
            }
            catch (Exception ex)
            {
                DiscountAmount = 0;
                SelectedDiscountCode = null;
            }
        }

        /// <summary>
        /// Restore discount code from order into session (for Pay Now flow)
        /// </summary>
        private async Task RestoreDiscountFromOrder(Guid discountCodeId, HttpClient client)
        {
            try
            {
                // Fetch discount code details from API
                var discountResponse = await client.GetAsync($"{backendBaseUrl}/api/discountcode/{discountCodeId}");
                
                if (discountResponse.IsSuccessStatusCode)
                {
                    var discountContent = await discountResponse.Content.ReadAsStringAsync();
                    var discountApiResponse = JsonSerializer.Deserialize<ApiResponse<DiscountCodeDto>>(
                        discountContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    var discountCode = discountApiResponse?.Data;
                    
                    if (discountCode != null)
                    {
                        // Create session info object
                        var sessionInfo = new DiscountCodeSessionInfo
                        {
                            Id = discountCode.Id.ToString(),
                            Code = discountCode.Code,
                            DiscountType = discountCode.DiscountType.ToString(),
                            Value = discountCode.Value,
                            UsageType = discountCode.UsageType.ToString()
                        };
                        
                        // Save to session
                        var sessionJson = JsonSerializer.Serialize(sessionInfo);
                        HttpContext.Session.SetString("SelectedDiscountCode", sessionJson);
                        
                        // Set for display
                        SelectedDiscountCode = sessionInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring discount from order: {ex.Message}");
                // Continue without discount if fetch fails
            }
        }
    }

    public class CheckoutInputModel
    {
        public bool UseSameProfile { get; set; } = true;

        public string? CustomerFullName { get; set; }
        public string? Email { get; set; }
        
        [Required(ErrorMessage = "Phone number is required")]
        public string? PhoneNumber { get; set; }
        
        [Required(ErrorMessage = "Address is required")]
        public string? Address { get; set; }
        
        public bool HasAgreedToPolicies { get; set; } = false;

        public string PaymentMethod { get; set; }
    }

    /// <summary>
    /// DTO for storing discount code info from session
    /// </summary>
    public class DiscountCodeSessionInfo
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string DiscountType { get; set; }
        public decimal Value { get; set; }
        public string UsageType { get; set; }
    }
}