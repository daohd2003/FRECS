using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Contact;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;

namespace ShareItFE.Pages
{
    //[Authorize(Roles = "customer,provider")]
    public class ContactModel : PageModel
    {
        private readonly ILogger<ContactModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        public ContactModel(ILogger<ContactModel> logger, AuthenticatedHttpClientHelper clientHelper)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            InitializeContactData();
        }

        [BindProperty]
        public ReportDTO FormInput { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }
        [TempData]
        public string? ErrorMessage { get; set; }

        public List<ContactMethod> ContactMethods { get; private set; } = new();
        public List<FaqCategory> FaqCategories { get; private set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                InitializeContactData();
                return Page();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                FormInput.ReporterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                FormInput.CreatedAt = DateTime.UtcNow;
                FormInput.Status = ReportStatus.open;

                var response = await client.PostAsJsonAsync("api/report", FormInput);

                if (response.IsSuccessStatusCode)
                {
                    var successResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                    SuccessMessage = successResponse?.Message ?? "Your report has been submitted successfully!";
                    FormInput = new ReportDTO();
                }
                else
                {
                    // Xử lý lỗi như cũ
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit report.");
                ErrorMessage = "Could not connect to the server. Please try again later.";
            }

            return RedirectToPage();
        }

        public async Task<JsonResult> OnGetSearchUsers(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return new JsonResult(new List<object>());
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                var response = await client.GetFromJsonAsync<ApiResponse<List<UserDto>>>("api/users/search-by-email");

                if (response?.Data == null)
                {
                    return new JsonResult(new List<object>());
                }

                var filteredUsers = response.Data
                    .Where(u =>
                        u.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        (u.FullName != null && u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .Select(u => new {
                        id = u.Id,
                        text = $"{u.Email}{(u.FullName != null ? $" ({u.FullName})" : "")}"
                    })
                    .ToList();

                return new JsonResult(filteredUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search users for term: {Term}", term);
                return new JsonResult(new List<object>());
            }
        }

        private void InitializeContactData()
        {
            ContactMethods = new List<ContactMethod>
            {
                new() {
                    IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><rect width=""20"" height=""16"" x=""2"" y=""4"" rx=""2""/><path d=""m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7""/></svg>",
                    Title = "Report Center",
                    Description = "Submit reports and get support",
                    Contact = "support@example.com",
                    Availability = "Response within 24 hours"
                },
                // Giữ nguyên các contact methods khác
            };

            FaqCategories = new List<FaqCategory>
            {
                // Giữ nguyên các FAQ categories
            };
        }
    }

    public class ReportTypeViewModel
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }






    //public class ContactModel : PageModel
    //{
    //    private readonly ILogger<ContactModel> _logger;
    //    private readonly AuthenticatedHttpClientHelper _clientHelper;

    //    public ContactModel(ILogger<ContactModel> logger, AuthenticatedHttpClientHelper clientHelper)
    //    {
    //        _logger = logger;
    //        _clientHelper = clientHelper;
    //        InitializeContactData();
    //    }

    //    [BindProperty]
    //    public ContactFormRequestDto FormInput { get; set; } = new();

    //    [TempData]
    //    public string? SuccessMessage { get; set; }
    //    [TempData]
    //    public string? ErrorMessage { get; set; }

    //    public List<ContactMethod> ContactMethods { get; private set; } = new();
    //    public List<FaqCategory> FaqCategories { get; private set; } = new();

    //    public void OnGet() { }

    //    public async Task<IActionResult> OnPostAsync()
    //    {
    //        if (!ModelState.IsValid)
    //        {
    //            InitializeContactData();
    //            return Page();
    //        }

    //        try
    //        {
    //            var client = await _clientHelper.GetAuthenticatedClientAsync();
    //            var response = await client.PostAsJsonAsync("api/contact", FormInput);

    //            if (response.IsSuccessStatusCode)
    //            {
    //                // Xử lý khi API trả về thành công (2xx)
    //                var successResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
    //                SuccessMessage = successResponse?.Message ?? "Message sent successfully!";
    //            }
    //            else
    //            {
    //                // Xử lý khi API trả về lỗi (4xx, 5xx)
    //                var errorContent = await response.Content.ReadAsStringAsync();
    //                _logger.LogWarning("API returned a non-success status code ({StatusCode}): {ErrorContent}", response.StatusCode, errorContent);

    //                // Cố gắng đọc lỗi theo định dạng ApiResponse chuẩn
    //                try
    //                {
    //                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    //                    ErrorMessage = errorResponse?.Message;
    //                }
    //                catch (JsonException)
    //                {
    //                    // Nếu không được, hiển thị thông báo chung chung hơn
    //                    ErrorMessage = $"An error occurred while processing your request. Please try again.";
    //                }

    //                // Nếu ErrorMessage vẫn rỗng, gán một giá trị mặc định
    //                if (string.IsNullOrEmpty(ErrorMessage))
    //                {
    //                    ErrorMessage = $"An error occurred. Status: {response.StatusCode}";
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Failed to call contact API.");
    //            ErrorMessage = "Could not connect to the server. Please try again later.";
    //        }

    //        return RedirectToPage();
    //    }

    //    private void InitializeContactData()
    //    {
    //        ContactMethods = new List<ContactMethod>
    //        {
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><rect width=""20"" height=""16"" x=""2"" y=""4"" rx=""2""/><path d=""m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7""/></svg>",
    //                Title = "Email Support", Description = "Get help via email", Contact = "support@frecs.com", Availability = "Response within 24 hours"
    //            },
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z""/></svg>",
    //                Title = "Phone Support", Description = "Speak with our team", Contact = "+1 (555) 123-FRECS", Availability = "Mon-Fri, 9AM-6PM EST"
    //            },
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z""/></svg>",
    //                Title = "Live Chat", Description = "Chat with us instantly", Contact = "Available on website", Availability = "Mon-Fri, 9AM-9PM EST"
    //            }
    //        };

    //        FaqCategories = new List<FaqCategory>
    //        {
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""32"" height=""32"" class=""text-blue-600"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><circle cx=""12"" cy=""12"" r=""10""/><path d=""M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3""/><path d=""M12 17h.01""/></svg>",
    //                Title = "General Questions", Description = "How FRECS works, account setup, and basic information", Link = "/Help/FAQ?category=general"
    //            },
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""32"" height=""32"" class=""text-green-600"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M5 18H3c-.6 0-1-.4-1-1V7c0-.6.4-1 1-1h10c.6 0 1 .4 1 1v11""/><path d=""M14 9h4l4 4v4h-8v-4l4-4z""/><circle cx=""7"" cy=""18"" r=""2""/><circle cx=""17"" cy=""18"" r=""2""/></svg>",
    //                Title = "Shipping & Returns", Description = "Delivery times, return process, and shipping policies", Link = "/Help/FAQ?category=shipping"
    //            },
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""32"" height=""32"" class=""text-purple-600"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><rect width=""20"" height=""14"" x=""2"" y=""5"" rx=""2""/><line x1=""2"" x2=""22"" y1=""10"" y2=""10""/></svg>",
    //                Title = "Billing & Payments", Description = "Payment methods, refunds, and billing questions", Link = "/Help/FAQ?category=billing"
    //            },
    //            new() {
    //                IconSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""32"" height=""32"" class=""text-red-600"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z""/></svg>",
    //                Title = "Safety & Security", Description = "Account security, damage protection, and safety policies", Link = "/Help/FAQ?category=security"
    //            }
    //        };
    //    }
    //}
}