using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
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

namespace ShareItFE.Pages.CheckoutPage
{
    public class CheckoutModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;

        public string frontendBaseUrl { get; set; }
        public string backendBaseUrl { get; set; }
        public CheckoutModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "/";
            backendBaseUrl = _configuration["BackendBaseUrl"] ?? "https://localhost:7256/";
        }

        [BindProperty]
        public CheckoutInputModel Input { get; set; } = new CheckoutInputModel();

        public CartDto? Cart { get; set; }
        public OrderDetailsDto? SingleOrder { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string QrCodeUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? OrderId { get; set; }

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

                // KIỂM TRA ĐẦU TIÊN: Nếu có OrderId từ "Rent Again", ưu tiên tải đơn hàng đó
                if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                {
                    // Call the API to get details of the specific order
                    // Ensure your API endpoint for OrderDetailsDto is correct, e.g., api/orders/{id}/details
                    var orderResponse = await client.GetAsync($"{backendBaseUrl}api/orders/{OrderId.Value}/details");

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

                        // Populate totals from the single order
                        Subtotal = SingleOrder.Items.Sum(item => item.PricePerDay * item.Quantity * item.RentalDays);
                        Total = Subtotal;
                        Input.UseSameProfile = true;
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
                            Subtotal = Cart.Items.Sum(item => item.PricePerUnit * item.RentalDays * item.Quantity);
                            // Giả định logic tính phí giao hàng và thuế
                            //DeliveryFee = Subtotal > 100000 ? 0 : 15000;
                            //Total = Subtotal + DeliveryFee ;
                            Total = Subtotal;
                        }
                    }
                    else
                    {
                        var errorContent = await cartResponse.Content.ReadAsStringAsync();
                        ErrorMessage = $"Could not load cart: {errorContent}";
                        return RedirectToPage("/CartPage/Cart");
                    }

                    // Lấy thông tin profile cho trang thanh toán
                    if (string.IsNullOrEmpty(Input.CustomerFullName) && Input.UseSameProfile)
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
                                Input.UseSameProfile = true;
                            }
                        }
                        else if (profileResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            Console.WriteLine("No profile found for this user. Manual input required.");
                            // Nếu không tìm thấy profile, mặc định bỏ chọn "UseSameProfile"
                            Input.UseSameProfile = false;
                        }
                        else
                        {
                            var errorContent = await profileResponse.Content.ReadAsStringAsync();
                            ErrorMessage = $"Could not load user profile: {errorContent}";
                            // Mặc định bỏ chọn "UseSameProfile" nếu có lỗi khi tải profile
                            Input.UseSameProfile = false;
                        }
                    }
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!Request.Form.ContainsKey("Input.HasAgreedToPolicies"))
            {
                Input.HasAgreedToPolicies = false;
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
                Input.HasAgreedToPolicies = currentInputState.HasAgreedToPolicies;
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
                    var profileResponse = await client.GetAsync($"{backendBaseUrl}api/profile/my-profile-for-checkout");
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

                // SỬA: Logic phân biệt giữa "Rent Again" (OrderId đã có) và "Cart Checkout"
                if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                {
                    // Case 1: Processing a single order from "Rent Again"
                    orderIdsToProcess.Add(OrderId.Value);

                    // Fetch the single order details again to get the accurate TotalAmount for payment
                    // (This ensures we have the latest total and status from the API)
                    var orderResponse = await client.GetAsync($"{backendBaseUrl}api/orders/{OrderId.Value}/details");
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
                            OrderId = OrderId.Value,
                            CustomerFullName = Input.CustomerFullName ?? "",
                            CustomerEmail = Input.Email ?? "",
                            CustomerPhoneNumber = Input.PhoneNumber ?? "",
                            DeliveryAddress = Input.Address ?? "",
                            HasAgreedToPolicies = Input.HasAgreedToPolicies
                        };

                        var updateInfoResponse = await client.PutAsJsonAsync($"{backendBaseUrl}api/orders/update-contact-info", updateContactInfoRequest);

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
                    // This section will create orders from the cart.
                    // This is where the call to api/cart/checkout is necessary.
                    var checkoutRequest = new CheckoutRequestDto
                    {
                        UseSameProfile = Input.UseSameProfile,
                        CustomerFullName = Input.CustomerFullName,
                        CustomerEmail = Input.Email,
                        CustomerPhoneNumber = Input.PhoneNumber,
                        DeliveryAddress = Input.Address,
                        HasAgreedToPolicies = Input.HasAgreedToPolicies
                    };

                    var cartCheckoutResponse = await client.PostAsJsonAsync($"{backendBaseUrl}api/cart/checkout", checkoutRequest);

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

                    var vnpayResponse = await client.PostAsJsonAsync($"{backendBaseUrl}api/payment/Vnpay/CreatePaymentUrl", vnpayRequest);

                    if (vnpayResponse.IsSuccessStatusCode)
                    {
                        var vnpayApiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(
                            await vnpayResponse.Content.ReadAsStringAsync(), options);
                        var paymentUrl = vnpayApiResponse?.Data;

                        if (!string.IsNullOrEmpty(paymentUrl))
                        {
                            SuccessMessage = "Redirecting to VNPay for payment...";
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

                    var qrResponse = await client.PostAsJsonAsync($"{backendBaseUrl}api/transactions/create", createTransactionRequest);

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

                            Input = new CheckoutInputModel();
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
    }

    public class CheckoutInputModel
    {
        public bool UseSameProfile { get; set; } = true;

        public string? CustomerFullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public bool HasAgreedToPolicies { get; set; } = false;

        public string PaymentMethod { get; set; }
    }
}