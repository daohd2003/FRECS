using BusinessObject.DTOs.TransactionDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class TransactionDetailModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public TransactionDetailModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public TransactionManagementDto Transaction { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            try
            {
                var token = Request.Cookies["AccessToken"];
                
                if (string.IsNullOrEmpty(token))
                {
                    return RedirectToPage("/Auth");
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                var apiBaseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"] ?? "https://localhost:7256/api";
                
                var transactionUrl = $"{apiBaseUrl}/TransactionManagement/{id}";
                var response = await client.GetAsync(transactionUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    Transaction = await response.Content.ReadFromJsonAsync<TransactionManagementDto>(jsonOptions);
                    
                    if (Transaction == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy giao dịch";
                        return RedirectToPage("/Admin/TransactionManagement");
                    }
                    
                    return Page();
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy giao dịch";
                    return RedirectToPage("/Admin/TransactionManagement");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tải chi tiết giao dịch: {ex.Message}";
                return RedirectToPage("/Admin/TransactionManagement");
            }
        }

        public string GetCategoryBadgeClass(TransactionCategory category)
        {
            return category switch
            {
                TransactionCategory.Purchase => "bg-success",
                TransactionCategory.Rental => "bg-primary",
                TransactionCategory.DepositRefund => "bg-info",
                TransactionCategory.ProviderWithdrawal => "bg-warning",
                TransactionCategory.Penalty => "bg-danger",
                TransactionCategory.Compensation => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        public string GetStatusBadgeClass(TransactionStatus status)
        {
            return status switch
            {
                TransactionStatus.completed => "bg-success",
                TransactionStatus.initiated => "bg-warning",
                TransactionStatus.failed => "bg-danger",
                _ => "bg-secondary"
            };
        }

        public string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0") + " ₫";
        }
    }
}
