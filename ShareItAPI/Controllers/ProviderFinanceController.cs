using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.ProviderFinanceServices;
using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Authorization;
using Common.Utilities;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,provider")]
    [ApiController]
    public class ProviderFinanceController : ControllerBase
    {
        private readonly IProviderFinanceService _financeService;
        private readonly UserContextHelper _userHelper;

        public ProviderFinanceController(IProviderFinanceService financeService, UserContextHelper userHelper)
        {
            _financeService = financeService;
            _userHelper = userHelper;
        }

        [HttpGet("{providerId}/summary")]
        public async Task<IActionResult> GetSummary(Guid providerId)
        {
            if (!_userHelper.IsAdmin() && !_userHelper.IsOwner(providerId))
                throw new InvalidOperationException("You are not authorized to access these bank accounts.");

            var revenue = await _financeService.GetTotalRevenue(providerId);
            var bank = await _financeService.GetPrimaryBankAccount(providerId);

            var result = new
            {
                ProviderId = providerId,
                TotalReceived = revenue,
                BankAccount = bank
            };

            return Ok(new ApiResponse<object>("Provider revenue summary fetched successfully.", result));
        }

        [HttpGet("{providerId}/transactions")]
        public async Task<IActionResult> GetTransactions(Guid providerId)
        {
            if (!_userHelper.IsAdmin() && !_userHelper.IsOwner(providerId))
                throw new InvalidOperationException("You are not authorized to access these bank accounts.");

            var transactions = await _financeService.GetTransactionDetails(providerId);
            return Ok(new ApiResponse<object>("Provider transaction list fetched successfully.", transactions));
        }

        /// <summary>
        /// Get summary statistics of all provider payments - Admin only
        /// Returns total amount owed and count only, no detailed provider list
        /// </summary>
        [HttpGet("all/summary")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllProviderPaymentsSummary()
        {
            var fullSummary = await _financeService.GetAllProviderPaymentsSummaryAsync();
            
            // Return only summary statistics, no provider details
            var summaryOnly = new
            {
                TotalAmountOwed = fullSummary.TotalAmountOwed,
                TotalProviders = fullSummary.TotalProviders,
                TopProviderEarnings = fullSummary.Providers.FirstOrDefault()?.TotalEarned ?? 0,
                AverageEarningsPerProvider = fullSummary.TotalProviders > 0 
                    ? fullSummary.TotalAmountOwed / fullSummary.TotalProviders 
                    : 0
            };
            
            return Ok(new ApiResponse<object>("Provider payments summary statistics fetched successfully.", summaryOnly));
        }

        /// <summary>
        /// Get detailed list of all provider payments with full information - Admin only
        /// Returns complete provider details including bank accounts
        /// Supports optional date filtering
        /// </summary>
        [HttpGet("all/payments")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllProviderPayments([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            var payments = await _financeService.GetAllProviderPaymentsAsync(startDate, endDate);
            return Ok(new ApiResponse<object>("All provider payments with details fetched successfully.", payments));
        }
    }
}