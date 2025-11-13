using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text.Json;
using System.Text;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.SystemConfigDto;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class CommissionRatesModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public CommissionRatesModel(
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        [BindProperty]
        public decimal RentalCommissionRate { get; set; }

        [BindProperty]
        public decimal PurchaseCommissionRate { get; set; }

        public DateTime LastUpdated { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                var response = await client.GetAsync($"{apiBaseUrl}/systemconfig/commission-rates");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<CommissionRatesDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Data != null)
                    {
                        RentalCommissionRate = apiResponse.Data.RentalCommissionRate;
                        PurchaseCommissionRate = apiResponse.Data.PurchaseCommissionRate;
                        LastUpdated = apiResponse.Data.LastUpdated;
                    }
                }
                else
                {
                    ErrorMessage = "Failed to load commission rates.";
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading commission rates: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (RentalCommissionRate < 0 || RentalCommissionRate > 100)
                {
                    ErrorMessage = "Rental commission rate must be between 0 and 100.";
                    return Page();
                }

                if (PurchaseCommissionRate < 0 || PurchaseCommissionRate > 100)
                {
                    ErrorMessage = "Purchase commission rate must be between 0 and 100.";
                    return Page();
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                var requestData = new
                {
                    RentalCommissionRate,
                    PurchaseCommissionRate
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PutAsync($"{apiBaseUrl}/systemconfig/commission-rates", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Commission rates updated successfully!";
                    // Reload the data
                    await OnGetAsync();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to update commission rates: {errorContent}";
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating commission rates: {ex.Message}";
                return Page();
            }
        }
    }
}
