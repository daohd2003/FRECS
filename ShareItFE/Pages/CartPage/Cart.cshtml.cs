using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.CartDto;
using BusinessObject.DTOs.ProductDto; // Ensure this is available for Product image and price
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonStringEnumConverter

namespace ShareItFE.Pages.CartPage
{
    public class CartModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        public CartModel(AuthenticatedHttpClientHelper clientHelper)
        {
            _clientHelper = clientHelper;
        }

        public CartDto? Cart { get; set; }
        public decimal Subtotal { get; set; }
        //public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Không tìm thấy ID người dùng.");
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
                        Subtotal = Cart.Items.Sum(item => item.PricePerUnit * item.RentalDays * item.Quantity);
                        //DeliveryFee = Subtotal > 100000 ? 0 : 15000; // Ví dụ: miễn phí giao hàng nếu tổng tiền > 100
                        //Total = Subtotal + DeliveryFee;
                        Total = Subtotal;
                    }
                    else
                    {
                        Cart = new CartDto { CustomerId = userId, Items = new List<CartItemDto>(), TotalAmount = 0 };
                        //Subtotal = 0; DeliveryFee = 0; Total = 0;
                        Subtotal = 0; Total = 0;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Không thể tải giỏ hàng: {errorContent}";
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToPage("/Auth");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã xảy ra lỗi không mong muốn: {ex.Message}";
            }
            return Page();
        }

        // --- NEW: Page Handler để cập nhật QUANTITY ---
        public async Task<IActionResult> OnPostUpdateQuantityAsync(Guid itemId, int currentQuantity, string action)
        {
            if (itemId == Guid.Empty)
            {
                ErrorMessage = "ID sản phẩm không hợp lệ.";
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
                ErrorMessage = "Hành động cập nhật số lượng không hợp lệ.";
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
                SuccessMessage = "Số lượng sản phẩm trong giỏ hàng đã được cập nhật thành công.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Không thể cập nhật số lượng: {errorContent}";
            }
            return RedirectToPage();
        }

        // --- RENAMED: Page Handler để cập nhật RENTAL DAYS ---
        public async Task<IActionResult> OnPostUpdateRentalDaysAsync(Guid itemId, int currentRentalDays, string action) // Đổi tên handler
        {
            if (itemId == Guid.Empty)
            {
                ErrorMessage = "ID sản phẩm không hợp lệ.";
                return RedirectToPage();
            }

            int newRentalDays = currentRentalDays;
            if (action == "increase")
            {
                newRentalDays = currentRentalDays + 1;
            }
            else if (action == "decrease")
            {
                newRentalDays = currentRentalDays - 1;
                if (newRentalDays < 1)
                {
                    // Nếu số ngày thuê về 0 hoặc nhỏ hơn, có thể đặt lại là 1 hoặc xóa item
                    newRentalDays = 1; // Đặt lại về 1 ngày tối thiểu
                    ErrorMessage = "Số ngày thuê phải ít nhất là 1.";
                }
            }
            else
            {
                ErrorMessage = "Hành động cập nhật số ngày thuê không hợp lệ.";
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
                SuccessMessage = "Số ngày thuê của sản phẩm trong giỏ hàng đã được cập nhật thành công.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Không thể cập nhật số ngày thuê: {errorContent}";
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
                SuccessMessage = "Sản phẩm đã được xóa khỏi giỏ hàng thành công.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Không thể xóa sản phẩm: {errorContent}";
            }
            return RedirectToPage();
        }
    }
}