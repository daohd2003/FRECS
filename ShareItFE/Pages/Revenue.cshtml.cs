using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.DTOs.WithdrawalDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ShareItFE.Pages
{
    // Note: [Authorize] removed to allow manual authentication check
    // This prevents 401 error and allows custom redirect logic for different user types
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
        public List<TopRevenueItemDto> TopRevenueItems { get; set; } = new List<TopRevenueItemDto>();
        public List<TopCustomerDto> TopCustomers { get; set; } = new List<TopCustomerDto>();

        [BindProperty]
        public CreateBankAccountDto NewBankAccount { get; set; } = new CreateBankAccountDto();

        [BindProperty]
        public WithdrawalRequestDto WithdrawalRequest { get; set; } = new WithdrawalRequestDto();

        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "month";

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "overview";

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

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
            // Silently redirect non-providers to home page without error message
            if (!string.Equals(UserRole, "provider", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            try
            {
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Build revenue stats URL with optional date filters
                var revenueUrl = $"api/revenue/stats?period={Period}";
                if (StartDate.HasValue && EndDate.HasValue)
                {
                    revenueUrl += $"&startDate={StartDate.Value:yyyy-MM-dd}&endDate={EndDate.Value:yyyy-MM-dd}";
                }

                // Execute all API calls in parallel for better performance
                var revenueTask = client.GetAsync(revenueUrl);
                var payoutTask = client.GetAsync("api/revenue/payout-summary");
                var bankTask = client.GetAsync("api/revenue/bank-accounts");
                var withdrawalHistoryTask = client.GetAsync("api/withdrawals/history");
                var balanceTask = client.GetAsync("api/withdrawals/available-balance");
                var topRevenueTask = client.GetAsync($"api/revenue/top-revenue?period={Period}&limit=5");
                var topCustomersTask = client.GetAsync($"api/revenue/top-customers?period={Period}&limit=5");

                // Wait for all requests to complete
                await Task.WhenAll(revenueTask, payoutTask, bankTask, withdrawalHistoryTask, balanceTask, topRevenueTask, topCustomersTask);

                // Process revenue statistics
                var revenueResponse = await revenueTask;

                // If any API returns 401/403, user doesn't have access - redirect to home
                if (revenueResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    revenueResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return RedirectToPage("/Index");
                }

                if (revenueResponse.IsSuccessStatusCode)
                {
                    var revenueJson = await revenueResponse.Content.ReadAsStringAsync();
                    RevenueStats = JsonSerializer.Deserialize<RevenueStatsDto>(revenueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RevenueStatsDto();
                }

                // Process payout summary
                var payoutResponse = await payoutTask;

                // Check for unauthorized access
                if (payoutResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    payoutResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return RedirectToPage("/Index");
                }

                if (payoutResponse.IsSuccessStatusCode)
                {
                    var payoutJson = await payoutResponse.Content.ReadAsStringAsync();
                    PayoutSummary = JsonSerializer.Deserialize<PayoutSummaryDto>(payoutJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PayoutSummaryDto();
                }

                // Process bank accounts
                var bankResponse = await bankTask;

                // Check for unauthorized access
                if (bankResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    bankResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return RedirectToPage("/Index");
                }

                if (bankResponse.IsSuccessStatusCode)
                {
                    var bankJson = await bankResponse.Content.ReadAsStringAsync();
                    var accounts = JsonSerializer.Deserialize<List<BankAccountDto>>(bankJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<BankAccountDto>();
                    // Sort: Primary accounts first, then by creation date
                    BankAccounts = accounts.OrderByDescending(a => a.IsPrimary).ToList();
                }

                // Process withdrawal history
                var withdrawalHistoryResponse = await withdrawalHistoryTask;

                // Check for unauthorized access
                if (withdrawalHistoryResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    withdrawalHistoryResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return RedirectToPage("/Index");
                }

                if (withdrawalHistoryResponse.IsSuccessStatusCode)
                {
                    var withdrawalJson = await withdrawalHistoryResponse.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<IEnumerable<WithdrawalHistoryDto>>>(withdrawalJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    WithdrawalHistory = apiResponse?.Data?.ToList() ?? new List<WithdrawalHistoryDto>();
                }

                // Process available balance
                var balanceResponse = await balanceTask;

                // Check for unauthorized access
                if (balanceResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    balanceResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return RedirectToPage("/Index");
                }

                if (balanceResponse.IsSuccessStatusCode)
                {
                    var balanceJson = await balanceResponse.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<decimal>>(balanceJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    AvailableBalance = apiResponse?.Data ?? 0;
                }

                // Process top revenue items
                var topRevenueResponse = await topRevenueTask;
                if (topRevenueResponse.IsSuccessStatusCode)
                {
                    var topRevenueJson = await topRevenueResponse.Content.ReadAsStringAsync();
                    TopRevenueItems = JsonSerializer.Deserialize<List<TopRevenueItemDto>>(topRevenueJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<TopRevenueItemDto>();
                }

                // Process top customers
                var topCustomersResponse = await topCustomersTask;
                if (topCustomersResponse.IsSuccessStatusCode)
                {
                    var topCustomersJson = await topCustomersResponse.Content.ReadAsStringAsync();
                    TopCustomers = JsonSerializer.Deserialize<List<TopCustomerDto>>(topCustomersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<TopCustomerDto>();
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                                                   ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // User doesn't have permission to access revenue data - redirect to home
                _logger.LogWarning(ex, "Unauthorized access attempt to revenue page by user {UserId} with role {Role}", userId, UserRole);
                return RedirectToPage("/Index");
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

        public async Task<IActionResult> OnPostExportReport()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToPage("/Auth");
                }

                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // Build revenue stats URL with date filters
                var revenueUrl = $"api/revenue/stats?period={Period}";
                if (StartDate.HasValue && EndDate.HasValue)
                {
                    revenueUrl += $"&startDate={StartDate.Value:yyyy-MM-dd}&endDate={EndDate.Value:yyyy-MM-dd}";
                }

                var response = await client.GetAsync(revenueUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Failed to export report. Please try again.";
                    return RedirectToPage(new { Tab = "overview", Period });
                }

                var json = await response.Content.ReadAsStringAsync();
                var stats = JsonSerializer.Deserialize<RevenueStatsDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (stats == null)
                {
                    ErrorMessage = "No data available to export.";
                    return RedirectToPage(new { Tab = "overview", Period });
                }

                // Generate CSV content
                var csv = new StringBuilder();
                csv.AppendLine("Revenue Report");
                csv.AppendLine($"Period,{Period}");

                // Calculate date range based on period or custom dates
                DateTime calculatedStartDate, calculatedEndDate;
                if (StartDate.HasValue && EndDate.HasValue)
                {
                    calculatedStartDate = StartDate.Value;
                    calculatedEndDate = EndDate.Value;
                }
                else
                {
                    var now = DateTime.Now;
                    switch (Period.ToLower())
                    {
                        case "week":
                            var dayOfWeek = (int)now.DayOfWeek;
                            var diff = dayOfWeek == 0 ? -6 : 1 - dayOfWeek; // Monday start
                            calculatedStartDate = now.Date.AddDays(diff);
                            calculatedEndDate = calculatedStartDate.AddDays(6); // Sunday end
                            break;
                        case "year":
                            calculatedStartDate = new DateTime(now.Year, 1, 1);
                            calculatedEndDate = new DateTime(now.Year, 12, 31);
                            break;
                        default: // month
                            calculatedStartDate = new DateTime(now.Year, now.Month, 1);
                            calculatedEndDate = calculatedStartDate.AddMonths(1).AddDays(-1);
                            break;
                    }
                }

                csv.AppendLine($"Date Range,{calculatedStartDate:dd-MM-yyyy} -> {calculatedEndDate:dd-MM-yyyy}");
                csv.AppendLine($"Generated,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csv.AppendLine();

                // Summary Section
                csv.AppendLine("SUMMARY");
                csv.AppendLine("Metric,Current Period,Previous Period,Growth %");
                csv.AppendLine($"Revenue (VND),{stats.CurrentPeriodRevenue:F0},{stats.PreviousPeriodRevenue:F0},{stats.RevenueGrowthPercentage:F2}");
                csv.AppendLine($"Orders,{stats.CurrentPeriodOrders},{stats.PreviousPeriodOrders},{stats.OrderGrowthPercentage:F2}");
                csv.AppendLine($"Net Revenue (VND),{stats.NetRevenue:F0},{stats.PreviousNetRevenue:F0},{stats.NetRevenueGrowthPercentage:F2}");
                csv.AppendLine($"Platform Fee (VND),{stats.PlatformFee:F0},{stats.PreviousPlatformFee:F0},{stats.PlatformFeeGrowthPercentage:F2}");
                csv.AppendLine($"Average Order Value (VND),{stats.AverageOrderValue:F0},{stats.PreviousAverageOrderValue:F0},{stats.AvgOrderValueGrowthPercentage:F2}");
                csv.AppendLine();

                // Chart Data Section
                if (stats.ChartData != null && stats.ChartData.Any())
                {
                    csv.AppendLine("DETAILED DATA");
                    csv.AppendLine("Period,Revenue (VND),Order Count");
                    foreach (var data in stats.ChartData)
                    {
                        csv.AppendLine($"{data.Period},{data.Revenue:F0},{data.OrderCount}");
                    }
                }

                // Return CSV file
                var fileName = $"Revenue_Report_{Period}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting revenue report");
                ErrorMessage = "An error occurred while exporting. Please try again.";
                return RedirectToPage(new { Tab = "overview", Period });
            }
        }
    }
}

