using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.DTOs.WithdrawalDto;
using BusinessObject.DTOs.ApiResponses;
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
        public List<WithdrawalHistoryDto> WithdrawalHistory { get; set; } = new List<WithdrawalHistoryDto>();
        public decimal AvailableBalance { get; set; } = 0;

        [BindProperty]
        public CreateBankAccountDto NewBankAccount { get; set; } = new CreateBankAccountDto();

        [BindProperty]
        public WithdrawalRequestDto WithdrawalRequest { get; set; } = new WithdrawalRequestDto();

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
            
            // Only providers can access revenue dashboard
            if (!string.Equals(UserRole, "provider", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Access denied. Revenue dashboard is only available for providers.";
                return RedirectToPage("/Index");
            }

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

                // Get bank accounts and sort by primary first
                var bankResponse = await client.GetAsync("api/revenue/bank-accounts");
                if (bankResponse.IsSuccessStatusCode)
                {
                    var bankJson = await bankResponse.Content.ReadAsStringAsync();
                    var accounts = JsonSerializer.Deserialize<List<BankAccountDto>>(bankJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<BankAccountDto>();
                    // Sort: Primary accounts first, then by creation date
                    BankAccounts = accounts.OrderByDescending(a => a.IsPrimary).ToList();
                }

                // Get withdrawal history (New system using WithdrawalRequests table)
                var withdrawalHistoryResponse = await client.GetAsync("api/withdrawals/history");
                if (withdrawalHistoryResponse.IsSuccessStatusCode)
                {
                    var withdrawalJson = await withdrawalHistoryResponse.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<IEnumerable<WithdrawalHistoryDto>>>(withdrawalJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    WithdrawalHistory = apiResponse?.Data?.ToList() ?? new List<WithdrawalHistoryDto>();
                }

                // Get available balance
                var balanceResponse = await client.GetAsync("api/withdrawals/available-balance");
                if (balanceResponse.IsSuccessStatusCode)
                {
                    var balanceJson = await balanceResponse.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<decimal>>(balanceJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    AvailableBalance = apiResponse?.Data ?? 0;
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
            // Only validate NewBankAccount fields
            var validationErrors = ModelState
                .Where(x => !x.Key.StartsWith("NewBankAccount."))
                .Select(x => x.Key)
                .ToList();

            foreach (var key in validationErrors)
            {
                ModelState.Remove(key);
            }
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ErrorMessage = string.Join(", ", errors);
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
                    ErrorMessage = string.Empty;
                    NewBankAccount = new CreateBankAccountDto(); // Reset form
                }
                else
                {
                    ErrorMessage = "Failed to add bank account. Please try again.";
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty;
            }

            return RedirectToPage(new { Tab = "payout" });
        }

        public async Task<IActionResult> OnPostUpdateBankAccountAsync(Guid accountId)
        {
            // Only validate NewBankAccount fields
            var validationErrors = ModelState
                .Where(x => !x.Key.StartsWith("NewBankAccount."))
                .Select(x => x.Key)
                .ToList();

            foreach (var key in validationErrors)
            {
                ModelState.Remove(key);
            }
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ErrorMessage = string.Join(", ", errors);
                return await OnGetAsync();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var json = JsonSerializer.Serialize(NewBankAccount);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"api/revenue/bank-accounts/{accountId}", content);
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Bank account updated successfully!";
                    ErrorMessage = string.Empty;
                }
                else
                {
                    ErrorMessage = "Failed to update bank account. Please try again.";
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank account");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty;
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
                    ErrorMessage = string.Empty;
                }
                else
                {
                    ErrorMessage = "Failed to delete bank account.";
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank account");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty;
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
                    ErrorMessage = string.Empty;
                }
                else
                {
                    ErrorMessage = "Failed to update primary account.";
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary account");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty;
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
                    ErrorMessage = string.Empty;
                }
                else
                {
                    ErrorMessage = "Failed to request payout. Please check your balance and try again.";
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting payout");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty;
            }

            return RedirectToPage(new { Tab = "payout" });
        }

        public async Task<IActionResult> OnPostRequestWithdrawalAsync()
        {
            // Only validate WithdrawalRequest fields
            var validationErrors = ModelState
                .Where(x => !x.Key.StartsWith("WithdrawalRequest."))
                .Select(x => x.Key)
                .ToList();

            foreach (var key in validationErrors)
            {
                ModelState.Remove(key);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ErrorMessage = string.Join(", ", errors);
                return await OnGetAsync();
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();
                var json = JsonSerializer.Serialize(WithdrawalRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/withdrawals/request", content);
                
                if (response.IsSuccessStatusCode)
                {
                    SuccessMessage = "Withdrawal request submitted successfully! Status: Initiated";
                    ErrorMessage = string.Empty; // Clear any previous error
                    WithdrawalRequest = new WithdrawalRequestDto(); // Reset form
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<string>>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ErrorMessage = errorResponse?.Message ?? "Failed to submit withdrawal request. Please try again.";
                    SuccessMessage = string.Empty; // Clear any previous success
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting withdrawal");
                ErrorMessage = "An error occurred. Please try again later.";
                SuccessMessage = string.Empty; // Clear any previous success
            }

            return RedirectToPage(new { Tab = "payout" });
        }
    }
}

