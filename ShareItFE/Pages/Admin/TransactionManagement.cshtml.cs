using BusinessObject.DTOs.TransactionDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Repositories.TransactionRepositories;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ShareItFE.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class TransactionManagementModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public TransactionManagementModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public List<TransactionManagementDto> Transactions { get; set; } = new();
        public TransactionStatisticsDto Statistics { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public TransactionCategory? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public TransactionStatus? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Debug all query parameters
                Console.WriteLine($"[TransactionManagement] === OnGetAsync START ===");
                Console.WriteLine($"[TransactionManagement] PageNumber parameter from binding: {PageNumber}");
                Console.WriteLine($"[TransactionManagement] SearchQuery: '{SearchQuery}'");
                Console.WriteLine($"[TransactionManagement] CategoryFilter: {CategoryFilter}");
                Console.WriteLine($"[TransactionManagement] StatusFilter: {StatusFilter}");
                
                CurrentPage = PageNumber > 0 ? PageNumber : 1;
                Console.WriteLine($"[TransactionManagement] CurrentPage set to: {CurrentPage}, PageSize: {PageSize}");
                
                var token = Request.Cookies["AccessToken"];
                
                if (string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Không tìm thấy token xác thực";
                    return RedirectToPage("/Auth");
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                var apiBaseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"] ?? "https://localhost:7256/api";

                // Build query string - only add non-empty parameters
                var queryParams = new List<string>
                {
                    $"PageNumber={CurrentPage}",
                    $"PageSize={PageSize}"
                };

                // Only add SearchQuery if it has value
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    queryParams.Add($"SearchQuery={Uri.EscapeDataString(SearchQuery)}");
                }
                
                if (StartDate.HasValue)
                    queryParams.Add($"StartDate={StartDate.Value:yyyy-MM-dd}");
                if (EndDate.HasValue)
                    queryParams.Add($"EndDate={EndDate.Value:yyyy-MM-dd}");
                if (CategoryFilter.HasValue)
                    queryParams.Add($"Category={(int)CategoryFilter.Value}");
                if (StatusFilter.HasValue)
                    queryParams.Add($"Status={(int)StatusFilter.Value}");

                var queryString = string.Join("&", queryParams);

                // Get transactions - Use full URL like other admin pages
                var transactionUrl = $"{apiBaseUrl}/TransactionManagement?{queryString}";
                
                Console.WriteLine($"[TransactionManagement] Calling API: {transactionUrl}");
                Console.WriteLine($"[TransactionManagement] Query Params: PageNumber={CurrentPage}, PageSize={PageSize}");
                
                var response = await client.GetAsync(transactionUrl);
                
                Console.WriteLine($"[TransactionManagement] Response Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    // Configure JSON options to handle enum as string
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    
                    var result = await response.Content.ReadFromJsonAsync<TransactionResponse>(jsonOptions);
                    if (result != null)
                    {
                        Transactions = result.Transactions ?? new List<TransactionManagementDto>();
                        TotalCount = result.TotalCount;
                        Console.WriteLine($"[TransactionManagement] Loaded {Transactions.Count} transactions, TotalCount: {TotalCount}, TotalPages: {TotalPages}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMsg = $"API Error: {response.StatusCode} - URL: {transactionUrl} - Response: {errorContent}";
                    Console.WriteLine($"[TransactionManagement] {errorMsg}");
                    TempData["ErrorMessage"] = errorMsg;
                }

                // Get statistics
                var statsQuery = "";
                if (StartDate.HasValue || EndDate.HasValue)
                {
                    var statsParams = new List<string>();
                    if (StartDate.HasValue)
                        statsParams.Add($"startDate={StartDate.Value:yyyy-MM-dd}");
                    if (EndDate.HasValue)
                        statsParams.Add($"endDate={EndDate.Value:yyyy-MM-dd}");
                    statsQuery = "?" + string.Join("&", statsParams);
                }

                var statsUrl = $"{apiBaseUrl}/TransactionManagement/statistics{statsQuery}";
                var statsResponse = await client.GetAsync(statsUrl);
                if (statsResponse.IsSuccessStatusCode)
                {
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    Statistics = await statsResponse.Content.ReadFromJsonAsync<TransactionStatisticsDto>(jsonOptions);
                }

                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tải dữ liệu: {ex.Message}. Stack: {ex.StackTrace}";
                return Page();
            }
        }

        private class TransactionResponse
        {
            public List<TransactionManagementDto> Transactions { get; set; } = new();
            public int TotalCount { get; set; }
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
