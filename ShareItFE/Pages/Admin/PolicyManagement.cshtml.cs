using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PolicyConfigDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Text;
using System.Text.Json;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class PolicyManagementModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public PolicyManagementModel(
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
        public string SuccessMessage { get; set; } = string.Empty;

        [BindProperty]
        public Guid? SelectedPolicyId { get; set; }

        [BindProperty]
        public string PolicyName { get; set; } = string.Empty;

        [BindProperty]
        public string PolicyContent { get; set; } = string.Empty;

        [BindProperty]
        public bool IsActive { get; set; } = true;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadPoliciesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                if (!SelectedPolicyId.HasValue || SelectedPolicyId == Guid.Empty)
                {
                    // Create new policy
                    var createDto = new CreatePolicyConfigDto
                    {
                        PolicyName = PolicyName,
                        Content = PolicyContent,
                        IsActive = IsActive
                    };

                    var json = JsonSerializer.Serialize(createDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync($"{apiBaseUrl}/policy-configs", content);

                    if (response.IsSuccessStatusCode)
                    {
                        SuccessMessage = "Policy created successfully!";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        ErrorMessage = $"Failed to create policy: {errorContent}";
                    }
                }
                else
                {
                    // Update existing policy
                    var updateDto = new UpdatePolicyConfigDto
                    {
                        PolicyName = PolicyName,
                        Content = PolicyContent,
                        IsActive = IsActive
                    };

                    var json = JsonSerializer.Serialize(updateDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PutAsync($"{apiBaseUrl}/policy-configs/{SelectedPolicyId}", content);

                    if (response.IsSuccessStatusCode)
                    {
                        SuccessMessage = "Policy updated successfully!";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        ErrorMessage = $"Failed to update policy: {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error saving policy: {ex.Message}";
            }

            await LoadPoliciesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                var response = await client.DeleteAsync($"{apiBaseUrl}/policy-configs/{id}");

                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Policy deleted successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to delete policy: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting policy: {ex.Message}";
            }

            await LoadPoliciesAsync();
            return Page();
        }

        private async Task LoadPoliciesAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var apiBaseUrl = _configuration.GetApiBaseUrl(_environment);

                var response = await client.GetAsync($"{apiBaseUrl}/policy-configs");

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
