using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DepositDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using ShareItFE.Extensions;
using System.Security.Claims;
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
        public List<RefundAccountDto> RefundAccounts { get; set; } = new();
        public List<DepositHistoryDto> DepositHistory { get; set; } = new();

        public string ApiBaseUrl => _configuration.GetApiBaseUrl(_environment);

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            try
            {
                await LoadDataFromApi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading deposit data");
                // Fallback to mock data if API fails
                LoadMockData();
            }

            return Page();
        }

        private async Task LoadDataFromApi()
        {
            var client = await _clientHelper.GetAuthenticatedClientAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1. Get deposit stats
            var statsResponse = await client.GetAsync("api/deposits/stats");
            if (statsResponse.IsSuccessStatusCode)
            {
                var statsContent = await statsResponse.Content.ReadAsStringAsync();
                var statsApiResponse = JsonSerializer.Deserialize<ApiResponse<DepositStatsDto>>(statsContent, options);
                Stats = statsApiResponse?.Data ?? new DepositStatsDto();
            }

            // 2. Get deposit history
            var historyResponse = await client.GetAsync("api/deposits/history");
            if (historyResponse.IsSuccessStatusCode)
            {
                var historyContent = await historyResponse.Content.ReadAsStringAsync();
                var historyApiResponse = JsonSerializer.Deserialize<ApiResponse<List<DepositHistoryDto>>>(historyContent, options);
                DepositHistory = historyApiResponse?.Data ?? new List<DepositHistoryDto>();
            }

            // 3. Load mock refund accounts (UI only - not implemented yet)
            LoadMockRefundAccounts();
        }

        private void LoadMockData()
        {
            // Mock deposit statistics
            Stats = new DepositStatsDto
            {
                DepositsRefunded = 0m,    // Not implemented - admin deposit refund system
                PendingRefunds = 0m,      // Not implemented - admin deposit refund system
                RefundIssues = 0          // Not implemented - admin deposit refund system
            };

            LoadMockRefundAccounts();

            // Mock deposit history - empty since deposit refund system not implemented
            DepositHistory = new List<DepositHistoryDto>();
        }

        private void LoadMockRefundAccounts()
        {
            // Mock refund accounts (UI only)
            RefundAccounts = new List<RefundAccountDto>
            {
                new RefundAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Chase Bank",
                    AccountNumber = "****1234",
                    AccountHolder = "RentChic LLC",
                    IsPrimary = true
                },
                new RefundAccountDto
                {
                    Id = Guid.NewGuid(),
                    BankName = "Bank of America",
                    AccountNumber = "****5678",
                    AccountHolder = "RentChic LLC",
                    IsPrimary = false
                }
            };
        }

        public async Task<IActionResult> OnPostFilterAsync()
        {
            // TODO: Implement filter functionality
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            // TODO: Implement export functionality
            TempData["SuccessMessage"] = "Export feature coming soon!";
            return RedirectToPage();
        }
    }
}

