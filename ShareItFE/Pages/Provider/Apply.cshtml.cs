using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProviderApplications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;

namespace ShareItFE.Pages.Provider
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ApplyModel(AuthenticatedHttpClientHelper clientHelper, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public string ApiBaseUrl { get; private set; } = string.Empty;

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

            public IFormFile? IdCardFrontImage { get; set; }
            public IFormFile? IdCardBackImage { get; set; }
            public IFormFile? SelfieImage { get; set; }
            public IFormFile? BusinessLicenseImage { get; set; }

            [Required(ErrorMessage = "You must agree to the privacy policy")]
            public bool PrivacyPolicyAgreed { get; set; } = false;
        }

        public void OnGet()
        {
            ApiBaseUrl = _configuration.GetApiRootUrl(_environment);

            // Clear ModelState và reset Input để tránh browser autofill từ cache
            ModelState.Clear();
            Input = new InputModel();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ApiBaseUrl = _configuration.GetApiRootUrl(_environment);
                return Page();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var root = _configuration.GetApiRootUrl(_environment);
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

            ApiBaseUrl = _configuration.GetApiRootUrl(_environment);
            return Page();
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostAjaxAsync()
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var root = _configuration.GetApiRootUrl(_environment);

                var form = Request.Form;
                var content = new MultipartFormDataContent();

                string GetValue(params string[] keys)
                {
                    foreach (var k in keys)
                    {
                        var v = form[k].ToString();
                        if (!string.IsNullOrWhiteSpace(v)) return v;
                    }
                    return string.Empty;
                }

                void AddString(string targetName, params string[] sourceKeys)
                {
                    var val = GetValue(sourceKeys);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        content.Add(new StringContent(val), targetName);
                    }
                }

                AddString("BusinessName", "BusinessName", "Input.BusinessName");
                AddString("TaxId", "TaxId", "Input.TaxId");
                AddString("ContactPhone", "ContactPhone", "Input.ContactPhone");
                AddString("Notes", "Notes", "Input.Notes");

                // Add ProviderType based on TaxId length
                var taxId = GetValue("TaxId", "Input.TaxId");
                var taxIdClean = new string(taxId.Where(char.IsDigit).ToArray());
                var providerType = taxIdClean.Length == 12 ? "Individual" : "Business";
                content.Add(new StringContent(providerType), "ProviderType");

                // Add PrivacyPolicyAgreed
                var privacyAgreed = GetValue("PrivacyPolicyAgreed", "Input.PrivacyPolicyAgreed");
                // Checkbox sends "on" or "true" when checked, empty when unchecked
                var isAgreed = !string.IsNullOrWhiteSpace(privacyAgreed) &&
                               (privacyAgreed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                privacyAgreed.Equals("on", StringComparison.OrdinalIgnoreCase));
                content.Add(new StringContent(isAgreed ? "true" : "false"), "PrivacyPolicyAgreed");

                IFormFile? TryGetFile(params string[] keys)
                {
                    foreach (var k in keys)
                    {
                        var f = form.Files[k];
                        if (f != null && f.Length > 0) return f;
                    }
                    return null;
                }

                var front = TryGetFile("IdCardFrontImage", "Input.IdCardFrontImage");
                var back = TryGetFile("IdCardBackImage", "Input.IdCardBackImage");
                var selfie = TryGetFile("SelfieImage", "Input.SelfieImage");
                var businessLicense = TryGetFile("BusinessLicenseImage", "Input.BusinessLicenseImage");

                if (front != null)
                {
                    var sc = new StreamContent(front.OpenReadStream());
                    sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(front.ContentType ?? "application/octet-stream");
                    content.Add(sc, "IdCardFrontImage", front.FileName);
                }
                if (back != null)
                {
                    var sc = new StreamContent(back.OpenReadStream());
                    sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(back.ContentType ?? "application/octet-stream");
                    content.Add(sc, "IdCardBackImage", back.FileName);
                }
                if (selfie != null)
                {
                    var sc = new StreamContent(selfie.OpenReadStream());
                    sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(selfie.ContentType ?? "application/octet-stream");
                    content.Add(sc, "SelfieImage", selfie.FileName);
                }
                if (businessLicense != null)
                {
                    var sc = new StreamContent(businessLicense.OpenReadStream());
                    sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(businessLicense.ContentType ?? "application/octet-stream");
                    content.Add(sc, "BusinessLicenseImage", businessLicense.FileName);
                }

                var res = await client.PostAsync($"{root}/api/provider-applications", content);
                var text = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, text);
                }
                return Content(text, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}



