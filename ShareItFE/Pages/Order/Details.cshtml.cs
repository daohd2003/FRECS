using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
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


    }


}