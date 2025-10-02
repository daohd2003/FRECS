using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ShareItFE.Pages.Admin.Discounts
{
    [Authorize(Roles = "admin")]
    public class DiscountCodesModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _httpClientHelper;

        public DiscountCodesModel(AuthenticatedHttpClientHelper httpClientHelper)
        {
            _httpClientHelper = httpClientHelper;
        }

        public void OnGet()
        {
            // Page will load data via JavaScript API calls
        }

        public async Task<IActionResult> OnPostCreateDiscountCodeAsync([FromBody] CreateDiscountCodeDto discountCode)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return new JsonResult(new { success = false, message = "Invalid data", errors = ModelState });
                }

                var json = JsonSerializer.Serialize(discountCode);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.PostAsync("/api/DiscountCode", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to create discount code", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPutUpdateDiscountCodeAsync(Guid id, [FromBody] UpdateDiscountCodeDto discountCode)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return new JsonResult(new { success = false, message = "Invalid data", errors = ModelState });
                }

                var json = JsonSerializer.Serialize(discountCode);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.PutAsync($"/api/DiscountCode/{id}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to update discount code", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnDeleteDiscountCodeAsync(Guid id)
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"/api/DiscountCode/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    return new JsonResult(new { success = true, message = "Discount code deleted successfully" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = false, message = "Failed to delete discount code", error = errorContent });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetDiscountCodesAsync()
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync("/api/DiscountCode");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { 
                        success = false, 
                        message = $"API call failed with status {response.StatusCode}",
                        error = responseContent
                    });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetDiscountCodeAsync(Guid id)
        {
            try
            {
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"/api/DiscountCode/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "Discount code not found" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetCheckCodeUniqueAsync(string code, Guid? excludeId = null)
        {
            try
            {
                var url = $"DiscountCode/check-unique/{code}";
                if (excludeId.HasValue)
                {
                    url += $"?excludeId={excludeId}";
                }
                
                var client = await _httpClientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"/api/{url}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = responseContent });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "Failed to check code uniqueness" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateDiscountCodeDto
    {
        [Required(ErrorMessage = "Discount code is required")]
        [StringLength(50, ErrorMessage = "Discount code cannot exceed 50 characters")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Value is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }

        [Required(ErrorMessage = "Expiration date is required")]
        public DateTime ExpirationDate { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public string Status { get; set; } = "Active";
    }

    public class UpdateDiscountCodeDto
    {
        [Required(ErrorMessage = "Discount code is required")]
        [StringLength(50, ErrorMessage = "Discount code cannot exceed 50 characters")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        public string DiscountType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Value is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }

        [Required(ErrorMessage = "Expiration date is required")]
        public DateTime ExpirationDate { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public string Status { get; set; } = "Active";
    }
}