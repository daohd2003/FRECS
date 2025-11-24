using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.BankAccounts;
using Common.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ProviderBankServices;

namespace ShareItAPI.Controllers
{
    [Route("api/provider/bank-accounts")]
    [Authorize(Roles = "admin,provider")]
    [ApiController]
    public class ProviderBankAccountController : ControllerBase
    {
        private readonly IProviderBankService _service;
        private readonly UserContextHelper _userHelper;

        public ProviderBankAccountController(IProviderBankService service, UserContextHelper userHelper)
        {
            _service = service;
            _userHelper = userHelper;
        }

        [HttpGet("{providerId}")]
        public async Task<IActionResult> GetByProviderId(Guid providerId)
        {
            if (!_userHelper.IsAdmin() && !_userHelper.IsOwner(providerId))
                throw new InvalidOperationException("You are not authorized to access these bank accounts.");

            var accounts = await _service.GetBankAccounts(providerId);
            return Ok(new ApiResponse<object>("Bank accounts retrieved", accounts));
        }

        [HttpGet("detail/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var account = await _service.GetBankAccountById(id);
            if (account == null)
                return NotFound(new ApiResponse<object>("Bank account not found", null));

            if (!_userHelper.IsAdmin() && !_userHelper.IsOwner(account.UserId))
                throw new InvalidOperationException("You are not authorized to access these bank accounts.");

            return Ok(new ApiResponse<object>("Bank account found", account));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BankAccountCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new ApiResponse<object>(string.Join(" ", errors), null));
            }

            var providerId = _userHelper.GetCurrentUserId();
            await _service.AddBankAccount(providerId, dto);
            return Ok(new ApiResponse<object>("Bank account added", null));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BankAccountUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new ApiResponse<object>(string.Join(" ", errors), null));
            }

            var providerId = _userHelper.GetCurrentUserId();
            var success = await _service.UpdateBankAccount(providerId, dto);
            if (!success)
                return NotFound(new ApiResponse<object>("Bank account not found", null));

            return Ok(new ApiResponse<object>("Bank account updated", null));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var providerId = _userHelper.GetCurrentUserId();
            var success = await _service.DeleteBankAccount(providerId, id);
            if (!success)
                return NotFound(new ApiResponse<object>("Bank account not found or you don't have permission to delete it", null));

            return Ok(new ApiResponse<object>("Bank account has been deleted.", null));
        }
    }
}