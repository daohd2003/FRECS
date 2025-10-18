using BusinessObject.DTOs.DepositDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace ShareItFE.Pages.Customer
{
    public class DepositManagementModel : PageModel
    {
        private readonly ILogger<DepositManagementModel> _logger;

        public DepositManagementModel(ILogger<DepositManagementModel> logger)
        {
            _logger = logger;
        }

        public DepositStatsDto Stats { get; set; }
        public List<RefundAccountDto> RefundAccounts { get; set; } = new();
        public List<DepositHistoryDto> DepositHistory { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToPage("/Auth");

            // TODO: Call API to get actual data
            // For now, using mock data for UI demonstration
            LoadMockData();

            return Page();
        }

        private void LoadMockData()
        {
            // Mock deposit statistics
            Stats = new DepositStatsDto
            {
                DepositsRefunded = 735.00m,
                PendingRefunds = 450.00m,
                RefundIssues = 1
            };

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

            // Mock deposit history
            DepositHistory = new List<DepositHistoryDto>
            {
                new DepositHistoryDto
                {
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD001",
                    ItemName = "Sarah's Designer Collection",
                    DepositAmount = 735.00m,
                    RefundMethod = "****1234",
                    RefundAmount = 735.00m,
                    Status = TransactionStatus.completed,
                    RefundDate = new DateTime(2024, 2, 25)
                },
                new DepositHistoryDto
                {
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD002",
                    ItemName = "Elite Fashion Rentals",
                    DepositAmount = 450.00m,
                    RefundMethod = "****5678",
                    RefundAmount = 450.00m,
                    Status = TransactionStatus.initiated,
                    RefundDate = new DateTime(2024, 2, 28)
                },
                new DepositHistoryDto
                {
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD003",
                    ItemName = "Luxury Wardrobe Co.",
                    DepositAmount = 150.00m,
                    RefundMethod = "****9012",
                    RefundAmount = 150.00m,
                    Status = TransactionStatus.failed,
                    RefundDate = new DateTime(2024, 2, 28)
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

