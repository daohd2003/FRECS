using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Order
{
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