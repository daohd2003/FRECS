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
    }
}