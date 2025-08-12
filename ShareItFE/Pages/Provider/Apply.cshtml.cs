using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProviderApplications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;

namespace ShareItFE.Pages.Provider
{
    public class ApplyModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;

        public ApplyModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [MaxLength(255)]
            public string BusinessName { get; set; } = string.Empty;

            [MaxLength(255)]
            public string? TaxId { get; set; }

            [MaxLength(255)]
            public string? ContactPhone { get; set; }

            [MaxLength(500)]
            public string? Notes { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var root = _configuration["ApiSettings:RootUrl"] ?? _configuration["ApiSettings:BaseUrl"];
                var dto = new ProviderApplicationCreateDto
                {
                    BusinessName = Input.BusinessName,
                    TaxId = Input.TaxId,
                    ContactPhone = Input.ContactPhone,
                    Notes = Input.Notes
                };

                var response = await client.PostAsJsonAsync($"{root}/api/provider-applications", dto);
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Application submitted. We will notify you after review.";
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to submit: {content}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            return Page();
        }
    }
}


