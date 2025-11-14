using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PolicyConfigDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text.Json;

namespace ShareItFE.Pages
{
    public class PoliciesModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public PoliciesModel(
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public List<PolicyConfigDto> Policies { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadActivePoliciesAsync();
            return Page();
        }

        private async Task LoadActivePoliciesAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                var response = await client.GetAsync($"{apiBaseUrl}/policy-configs/active");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<PolicyConfigDto>>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Data != null)
                    {
                        Policies = apiResponse.Data;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading policies: {ex.Message}";
            }
        }
    }
}
