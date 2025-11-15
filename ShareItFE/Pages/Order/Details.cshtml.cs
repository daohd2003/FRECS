using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text;

namespace ShareItFE.Pages.Order
{
    [Authorize(Roles = "customer,provider")]
    public class DetailsModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;


        public DetailsModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        [BindProperty]
        public OrderDetailsDto Order { get; set; }
        
        [BindProperty]
        public List<RentalViolationDto> Violations { get; set; } = new List<RentalViolationDto>();
        
        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);
        


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            // Verify user has customer or provider role (providers can also be customers)
            if (!User.IsInRole("customer") && !User.IsInRole("provider"))
            {
                TempData["ErrorMessage"] = "Access Denied. Only customers and providers can view order details.";
                return RedirectToPage("/Index");
            }

            try
            {
                // 1. Lấy HttpClient đã được xác thực
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // 2. Gọi đến endpoint mới trên API
                var response = await client.GetAsync($"api/orders/{id}/details");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDetailsDto>>(_jsonOptions);
                    if (apiResponse != null)
                    {
                        Order = apiResponse.Data;
                        if (Order == null)
                        {
                            return NotFound();
                        }
                        
                        // Verify the logged-in customer owns this order
                        var currentCustomerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (string.IsNullOrEmpty(currentCustomerId))
                        {
                            TempData["ErrorMessage"] = "User not authenticated.";
                            return RedirectToPage("/Auth");
                        }

                        // Check if the order belongs to the current customer by comparing CustomerId from API
                        // Note: We need to add CustomerId to OrderDetailsDto or verify through API
                        // For now, we'll rely on the API endpoint to only return orders for the authenticated user
                        
                        return Page();
                    }
                    else
                    {
                        // Xử lý trường hợp API trả về success=false
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Failed to load order details.";
                        return RedirectToPage("/Profile", new { tab = "orders" });
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    // Xử lý các lỗi HTTP khác
                    TempData["ErrorMessage"] = $"Error loading order: {response.ReasonPhrase}";
                    return RedirectToPage("/Profile", new { tab = "orders" });
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi kết nối hoặc lỗi ngoài lệ khác
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return RedirectToPage("/Profile", new { tab = "orders" });
            }
        }

        public async Task<IActionResult> OnGetViolationsAsync(Guid orderId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"api/rental-violations/order/{orderId}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<RentalViolationDto>>>(_jsonOptions);
                    if (apiResponse != null && apiResponse.Data != null)
                    {
                        Violations = apiResponse.Data.ToList();
                        return new JsonResult(new { success = true, violations = Violations });
                    }
                }
                
                return new JsonResult(new { success = false, message = "Failed to load violations" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostRespondToViolationAsync()
        {
            try
            {
                // Get parameters from query string and form
                var violationIdStr = Request.Query["violationId"].ToString();
                var isAcceptedStr = Request.Query["isAccepted"].ToString();
                var customerNotes = Request.Form["customerNotes"].ToString();
                
                if (!Guid.TryParse(violationIdStr, out Guid violationId))
                {
                    return new JsonResult(new { success = false, message = "Invalid violation ID" });
                }
                
                if (!bool.TryParse(isAcceptedStr, out bool isAccepted))
                {
                    return new JsonResult(new { success = false, message = "Invalid isAccepted value" });
                }
                
                Console.WriteLine($"DEBUG: violationId={violationId}, isAccepted={isAccepted}, customerNotes={customerNotes}");
                
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                
                // Prepare the request data
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(isAccepted.ToString().ToLower()), "IsAccepted");
                
                if (!isAccepted && !string.IsNullOrEmpty(customerNotes))
                {
                    formData.Add(new StringContent(customerNotes), "CustomerNotes");
                }

                Console.WriteLine($"DEBUG: Calling API endpoint: api/rental-violations/{violationId}/respond");
                var response = await client.PostAsync($"api/rental-violations/{violationId}/respond", formData);
                Console.WriteLine($"DEBUG: API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>(_jsonOptions);
                    return new JsonResult(new { 
                        success = true, 
                        message = apiResponse?.Message ?? (isAccepted ? "Compensation request accepted" : "Rejection response sent")
                    });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DEBUG: API Error Response: {errorContent}");
                    
                    var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>(_jsonOptions);
                    return new JsonResult(new { 
                        success = false, 
                        message = errorResponse?.Message ?? $"API Error: {response.StatusCode} - {errorContent}"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception in OnPostRespondToViolationAsync: {ex}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

    }


}