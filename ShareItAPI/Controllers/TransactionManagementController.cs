using BusinessObject.DTOs.TransactionDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.TransactionRepositories;
using Services.TransactionManagementServices;
using System;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class TransactionManagementController : ControllerBase
    {
        private readonly ITransactionManagementService _service;

        public TransactionManagementController(ITransactionManagementService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get all transactions with filters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter)
        {
            try
            {
                var (transactions, totalCount) = await _service.GetAllTransactionsAsync(filter);
                return Ok(new
                {
                    transactions,
                    totalCount,
                    pageNumber = filter.PageNumber,
                    pageSize = filter.PageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving transactions: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get transaction detail by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransactionDetail(Guid id)
        {
            try
            {
                var transaction = await _service.GetTransactionDetailAsync(id);
                if (transaction == null)
                {
                    return NotFound(new { message = "Transaction not found" });
                }
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving transaction detail: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get transaction statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var statistics = await _service.GetTransactionStatisticsAsync(startDate, endDate);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving statistics: {ex.Message}" });
            }
        }
    }
}
