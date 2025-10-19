using BusinessObject.DTOs.RevenueDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Services.CustomerBankServices;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [Route("api/customer/banks")]
    [ApiController]
    [Authorize(Roles = "customer")]
    public class CustomerBankController : ControllerBase
    {
        private readonly ICustomerBankService _customerBankService;
        private readonly ILogger<CustomerBankController> _logger;

        public CustomerBankController(ICustomerBankService customerBankService, ILogger<CustomerBankController> logger)
        {
            _customerBankService = customerBankService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        [HttpGet]
        public async Task<ActionResult<List<BankAccountDto>>> GetBankAccounts()
        {
            try
            {
                var customerId = GetCurrentUserId();
                if (customerId == Guid.Empty)
                {
                    return Unauthorized("Customer not found");
                }

                var accounts = await _customerBankService.GetBankAccountsAsync(customerId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bank accounts for customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<BankAccountDto>> CreateBankAccount([FromBody] CreateBankAccountDto dto)
        {
            try
            {
                var customerId = GetCurrentUserId();
                if (customerId == Guid.Empty)
                {
                    return Unauthorized("Customer not found");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var account = await _customerBankService.CreateBankAccountAsync(customerId, dto);
                return CreatedAtAction(nameof(GetBankAccounts), account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account for customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{accountId}")]
        public async Task<ActionResult> UpdateBankAccount(Guid accountId, [FromBody] CreateBankAccountDto dto)
        {
            try
            {
                var customerId = GetCurrentUserId();
                if (customerId == Guid.Empty)
                {
                    return Unauthorized("Customer not found");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _customerBankService.UpdateBankAccountAsync(customerId, accountId, dto);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank account for customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{accountId}")]
        public async Task<ActionResult> DeleteBankAccount(Guid accountId)
        {
            try
            {
                var customerId = GetCurrentUserId();
                if (customerId == Guid.Empty)
                {
                    return Unauthorized("Customer not found");
                }

                var success = await _customerBankService.DeleteBankAccountAsync(customerId, accountId);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank account for customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{accountId}/set-primary")]
        public async Task<ActionResult> SetPrimaryBankAccount(Guid accountId)
        {
            try
            {
                var customerId = GetCurrentUserId();
                if (customerId == Guid.Empty)
                {
                    return Unauthorized("Customer not found");
                }

                var success = await _customerBankService.SetPrimaryBankAccountAsync(customerId, accountId);
                if (!success)
                {
                    return NotFound("Bank account not found");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary bank account for customer");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
