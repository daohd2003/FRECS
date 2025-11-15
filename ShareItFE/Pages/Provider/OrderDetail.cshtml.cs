using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Provider
{
    [Authorize] // Require authentication but check role manually in methods
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
        
        public List<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto> ExistingViolations { get; set; } = new List<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto>();
        public bool HasExistingViolations => ExistingViolations.Any();
        
        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth");
            }

            // Allow provider, staff, and admin roles
            if (!User.IsInRole("provider") && !User.IsInRole("staff") && !User.IsInRole("admin"))
            {
                TempData["ErrorMessage"] = "Access Denied. You do not have permission to access this page.";
                return RedirectToPage("/Index");
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

                        // Verify the logged-in provider owns this order (skip for staff/admin)
                        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var isStaffOrAdmin = User.IsInRole("staff") || User.IsInRole("admin");
                        
                        if (!isStaffOrAdmin && (string.IsNullOrEmpty(currentUserId) || Order.ProviderId.ToString() != currentUserId))
                        {
                            TempData["ErrorMessage"] = "You do not have permission to view this order.";
                            return RedirectToPage("/Provider/OrderManagement");
                        }

                        // Load existing violations for this order
                        try
                        {
                            var violationsResponse = await client.GetAsync($"api/rental-violations/order/{id}");
                            if (violationsResponse.IsSuccessStatusCode)
                            {
                                var violationsApiResponse = await violationsResponse.Content.ReadFromJsonAsync<BusinessObject.DTOs.ApiResponses.ApiResponse<IEnumerable<BusinessObject.DTOs.RentalViolationDto.RentalViolationDto>>>(_jsonOptions);
                                if (violationsApiResponse?.Data != null)
                                {
                                    ExistingViolations = violationsApiResponse.Data.ToList();
                                }
                            }
                        }
                        catch
                        {
                            // Ignore errors when fetching violations - not critical for page load
                        }

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
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "You do not have permission to view this order.";
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

