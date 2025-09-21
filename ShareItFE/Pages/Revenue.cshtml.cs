using BusinessObject.DTOs.RevenueDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text.Json;
using System.Text;

namespace ShareItFE.Pages
{
    [Authorize]
    public class RevenueModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;
        private readonly ILogger<RevenueModel> _logger;

        public RevenueModel(AuthenticatedHttpClientHelper clientHelper, ILogger<RevenueModel> logger)
        {
            _clientHelper = clientHelper;
            _logger = logger;
        }

        public RevenueStatsDto RevenueStats { get; set; } = new RevenueStatsDto();
        public PayoutSummaryDto PayoutSummary { get; set; } = new PayoutSummaryDto();
        public List<BankAccountDto> BankAccounts { get; set; } = new List<BankAccountDto>();
        public List<PayoutHistoryDto> PayoutHistory { get; set; } = new List<PayoutHistoryDto>();

        [BindProperty]
        public CreateBankAccountDto NewBankAccount { get; set; } = new CreateBankAccountDto();

        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "month";

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "overview";

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public string UserRole { get; set; } = "customer";

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Auth");
            }

            // Determine user role
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "customer";

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Get revenue statistics
                var revenueResponse = await client.GetAsync($"api/revenue/stats?period={Period}");
                if (revenueResponse.IsSuccessStatusCode)
                {
                    var revenueJson = await revenueResponse.Content.ReadAsStringAsync();
                    RevenueStats = JsonSerializer.Deserialize<RevenueStatsDto>(revenueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RevenueStatsDto();
                }

                // Get payout summary
                var payoutResponse = await client.GetAsync("api/revenue/payout-summary");
                if (payoutResponse.IsSuccessStatusCode)
                {
                    var payoutJson = await payoutResponse.Content.ReadAsStringAsync();
                    PayoutSummary = JsonSerializer.Deserialize<PayoutSummaryDto>(payoutJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PayoutSummaryDto();
                }

                // Get bank accounts
                var bankResponse = await client.GetAsync("api/revenue/bank-accounts");
                if (bankResponse.IsSuccessStatusCode)
                {
                    var bankJson = await bankResponse.Content.ReadAsStringAsync();
                    BankAccounts = JsonSerializer.Deserialize<List<BankAccountDto>>(bankJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<BankAccountDto>();
                }

                // Get payout history
                var historyResponse = await client.GetAsync("api/revenue/payout-history");
                if (historyResponse.IsSuccessStatusCode)
                {
                    var historyJson = await historyResponse.Content.ReadAsStringAsync();
                    PayoutHistory = JsonSerializer.Deserialize<List<PayoutHistoryDto>>(historyJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PayoutHistoryDto>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading revenue data");
                ErrorMessage = "Unable to load revenue data. Please try again later.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateBankAccountAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please check your input and try again.";
                return await OnGetAsync();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var json = JsonSerializer.Serialize(NewBankAccount);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/revenue/bank-accounts", content);
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Bank account added successfully!";
                    NewBankAccount = new CreateBankAccountDto(); // Reset form
                }
                else
                {
                    ErrorMessage = "Failed to add bank account. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage(new { Tab = "payout" });
        }

        public async Task<IActionResult> OnPostDeleteBankAccountAsync(Guid accountId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.DeleteAsync($"api/revenue/bank-accounts/{accountId}");
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Bank account deleted successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to delete bank account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage(new { Tab = "payout" });
        }

        public async Task<IActionResult> OnPostSetPrimaryAccountAsync(Guid accountId)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var response = await client.PostAsync($"api/revenue/bank-accounts/{accountId}/set-primary", null);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Primary account updated successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to update primary account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary account");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage(new { Tab = "payout" });
        }

        public async Task<IActionResult> OnPostRequestPayoutAsync(decimal amount)
        {
            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var json = JsonSerializer.Serialize(new { amount });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/revenue/request-payout", content);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Payout request submitted successfully!";
                }
                else
                {
                    ErrorMessage = "Failed to request payout. Please check your balance and try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting payout");
                ErrorMessage = "An error occurred. Please try again later.";
            }

            return RedirectToPage(new { Tab = "payout" });
        }
    }
}

