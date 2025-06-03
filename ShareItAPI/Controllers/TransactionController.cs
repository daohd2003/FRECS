using Azure.Core;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.BankQR;
using BusinessObject.DTOs.TransactionsDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.Transactions;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "customer")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly BankQrConfig _bankQrConfig;

        public TransactionController(ITransactionService transactionService, IOptions<BankQrConfig> bankQrOptions)
        {
            _transactionService = transactionService;
            _bankQrConfig = bankQrOptions.Value;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyTransactions()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
                return Unauthorized(new ApiResponse<string>("Unable to identify user.", null));

            var transactions = await _transactionService.GetUserTransactionsAsync(customerId);
            return Ok(new ApiResponse<object>("Success", transactions));
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
                return Unauthorized(new ApiResponse<string>("Unable to identify user.", null));

            var transaction = new BusinessObject.Models.Transaction
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                ProviderId = request.ProviderId,
                CustomerId = customerId,
                Amount = request.Amount,
                Status = BusinessObject.Enums.TransactionStatus.initiated,
                PaymentMethod = "SEPAY",
                TransactionDate = DateTime.UtcNow,
                Content = $"PAY_ORDER_{request.OrderId}::{request.Content ?? "Order payment"}"
            };

            await _transactionService.SaveTransactionAsync(transaction);

            var base64Image = await GenerateQrCodeBase64((double)transaction.Amount, transaction.Content);

            var responseData = new
            {
                transaction.Id,
                transaction.OrderId,
                transaction.Status,
                transaction.Amount,
                QrImage = base64Image
            };

            return Ok(new ApiResponse<object>("Transaction created successfully", responseData));
        }

        [HttpPost("{transactionId}/pay")]
        public async Task<IActionResult> PayTransaction(Guid transactionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
                return Unauthorized(new ApiResponse<string>("Unable to identify user.", null));

            var transaction = await _transactionService.GetTransactionByIdAsync(transactionId);
            if (transaction == null || transaction.CustomerId != customerId)
                return NotFound(new ApiResponse<string>("Transaction not found or does not belong to the user.", null));

            if (transaction.Status != BusinessObject.Enums.TransactionStatus.initiated)
                return BadRequest(new ApiResponse<string>("Transaction has already been processed or cannot be paid.", null));

            var qrImageUrl = GenerateQrCodeUrl((double)transaction.Amount, transaction.Content ?? $"Payment - {transaction.Id}");

            var responseData = new
            {
                transaction.Id,
                transaction.OrderId,
                transaction.Status,
                transaction.Amount,
                QrImageUrl = qrImageUrl
            };

            return Ok(new ApiResponse<object>("Payment QR code generated successfully", responseData));
        }

        private async Task<string> GenerateQrCodeBase64(double amount, string description)
        {
            string url = GenerateQrCodeUrl(amount, description);

            using var httpClient = new HttpClient();
            var imageBytes = await httpClient.GetByteArrayAsync(url);
            return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        }

        private string GenerateQrCodeUrl(double amount, string description)
        {
            string bankCode = _bankQrConfig.BankCode;
            string acc = _bankQrConfig.AccountNumber;
            string template = _bankQrConfig.Template;
            string des = Uri.EscapeDataString(description);

            return $"https://qr.sepay.vn/img?bank={bankCode}&acc={acc}&template={template}&amount={amount}&des={des}";
        }
    }
}