using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DepositDto;
using BusinessObject.DTOs.DepositRefundDto;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareItFE.Pages.Customer
{
    public class DepositManagementModel : PageModel
    {
        private readonly ILogger<DepositManagementModel> _logger;
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public DepositManagementModel(
            ILogger<DepositManagementModel> logger,
            AuthenticatedHttpClientHelper clientHelper,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _clientHelper = clientHelper;
            _configuration = configuration;
            _environment = environment;
        }

        public DepositStatsDto Stats { get; set; } = new();
        public List<DepositHistoryDto> DepositHistory { get; set; } = new();
        public List<BankAccountDto> BankAccounts { get; set; } = new();
        public List<DepositRefundDto> DepositRefunds { get; set; } = new();

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            try
            {
                await LoadDataFromApi();
                await LoadBankAccountsFromApi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                // Fallback to mock data if API fails
                LoadMockData();
                BankAccounts = new List<BankAccountDto>();
            }

            return Page();
        }

        private async Task LoadBankAccountsFromApi()
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            var response = await client.GetAsync("api/customer/banks");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var accounts = JsonSerializer.Deserialize<List<BankAccountDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                }) ?? new List<BankAccountDto>();
                
                // Sort: Primary account first, then by creation date (newest first)
                BankAccounts = accounts.OrderByDescending(a => a.IsPrimary).ThenByDescending(a => a.CreatedAt).ToList();
            }
            else
            {
                BankAccounts = new List<BankAccountDto>();
            }
        }

        public async Task<IActionResult> OnPostCreateBankAccountAsync(string bankName, string accountNumber, string accountHolderName, string routingNumber, bool setAsPrimary)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                
                var bankAccountDto = new CreateBankAccountDto
                {
                    BankName = bankName,
                    AccountNumber = accountNumber,
                    AccountHolderName = accountHolderName,
                    RoutingNumber = string.IsNullOrEmpty(routingNumber) ? null : routingNumber,
                    SetAsPrimary = setAsPrimary
                };

                var json = JsonSerializer.Serialize(bankAccountDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/customer/banks", content);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Bank account created successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to create bank account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateBankAccountAsync(Guid accountId, string bankName, string accountNumber, string accountHolderName, string routingNumber, bool setAsPrimary)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                
                var bankAccountDto = new CreateBankAccountDto
                {
                    BankName = bankName,
                    AccountNumber = accountNumber, 
                    AccountHolderName = accountHolderName,
                    RoutingNumber = string.IsNullOrEmpty(routingNumber) ? null : routingNumber,
                    SetAsPrimary = setAsPrimary
                };

                var json = JsonSerializer.Serialize(bankAccountDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"api/customer/banks/{accountId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Refund account updated successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to update refund account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating refund account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteBankAccountAsync(Guid accountId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"api/customer/banks/{accountId}");
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Refund account deleted successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to delete refund account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting refund account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetPrimaryBankAccountAsync(Guid accountId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.PostAsync($"api/customer/banks/{accountId}/set-primary", null);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Primary refund account updated successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to update primary refund account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary refund account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage();
        }

        private async Task LoadDataFromApi()
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1. Get deposit refunds (customer's refund history)
            var refundsResponse = await client.GetAsync("api/depositrefunds/my");
            if (refundsResponse.IsSuccessStatusCode)
            {
                var refundsContent = await refundsResponse.Content.ReadAsStringAsync();
                var refundsApiResponse = JsonSerializer.Deserialize<ApiResponse<List<DepositRefundDto>>>(refundsContent, options);
                DepositRefunds = refundsApiResponse?.Data ?? new List<DepositRefundDto>();
                
                // Calculate stats from deposit refunds
                CalculateStatsFromRefunds();
            }
            else
            {
                DepositRefunds = new List<DepositRefundDto>();
                Stats = new DepositStatsDto();
            }

            // 2. Get deposit history (legacy - can be deprecated if not needed)
            var historyResponse = await client.GetAsync("api/deposits/history");
            if (historyResponse.IsSuccessStatusCode)
            {
                var historyContent = await historyResponse.Content.ReadAsStringAsync();
                var historyApiResponse = JsonSerializer.Deserialize<ApiResponse<List<DepositHistoryDto>>>(historyContent, options);
                DepositHistory = historyApiResponse?.Data ?? new List<DepositHistoryDto>();
            }
        }

        private void CalculateStatsFromRefunds()
        {
            // Deposits Refunded: Sum of completed refunds
            Stats.DepositsRefunded = DepositRefunds
                .Where(r => r.Status == TransactionStatus.completed)
                .Sum(r => r.RefundAmount);

            // Pending Refunds: Sum of initiated (pending) refunds
            Stats.PendingRefunds = DepositRefunds
                .Where(r => r.Status == TransactionStatus.initiated)
                .Sum(r => r.RefundAmount);

            // Refund Issues: Count of failed refunds
            Stats.RefundIssues = DepositRefunds
                .Count(r => r.Status == TransactionStatus.failed);
        }

        private void LoadMockData()
        {
            // Mock deposit statistics
            Stats = new DepositStatsDto
            {
                DepositsRefunded = 0m,
                PendingRefunds = 0m,
                RefundIssues = 0
            };

            // Mock deposit refunds and history
            DepositRefunds = new List<DepositRefundDto>();
            DepositHistory = new List<DepositHistoryDto>();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            // TODO: Implement export functionality
            TempData["SuccessMessage"] = "Export feature coming soon!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetRefundDetailAsync(Guid refundId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.GetAsync($"api/depositrefunds/{refundId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return new JsonResult(new { success = true, data = content });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to load refund detail. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorContent);
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Failed to load refund detail. Status: {response.StatusCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading refund detail for ID: {RefundId}", refundId);
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}