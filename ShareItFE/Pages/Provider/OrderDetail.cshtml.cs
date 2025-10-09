using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Provider
{
    public class OrderDetailModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;

        public OrderDetailModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
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
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                // Use provider-specific endpoint
                var response = await client.GetAsync($"api/orders/{id}/provider-details");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDetailsDto>>(_jsonOptions);
                    if (apiResponse?.Data != null)
                    {
                        Order = apiResponse.Data;
                        return Page();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Failed to load order details.";
                        return RedirectToPage("/Provider/OrderManagement");
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToPage("/Provider/OrderManagement");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Error loading order: {response.ReasonPhrase}";
                    return RedirectToPage("/Provider/OrderManagement");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return RedirectToPage("/Provider/OrderManagement");
            }
        }
    }
}

