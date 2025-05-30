using Azure.Core;
using BusinessObject.DTOs.BankQR;
using BusinessObject.DTOs.TransactionsDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.Transactions;
using System.Security.Claims;
using System.Transactions;

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
                return Unauthorized("Không thể xác định người dùng.");

            var transactions = await _transactionService.GetUserTransactionsAsync(customerId);
            return Ok(transactions);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
                return Unauthorized("Không thể xác định người dùng.");

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
                Content = $"PAY_ORDER_{request.OrderId}::{request.Content ?? "Thanh toán đơn hàng"}"
            };

            await _transactionService.SaveTransactionAsync(transaction);

            var base64Image = await GenerateQrCodeBase64((double)transaction.Amount, transaction.Content);

            return Ok(new
            {
                transaction.Id,
                transaction.OrderId,
                transaction.Status,
                transaction.Amount,
                QrImage = base64Image
            });
        }

        [HttpPost("{transactionId}/pay")]
        public async Task<IActionResult> PayTransaction(Guid transactionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
                return Unauthorized("Không thể xác định người dùng.");

            var transaction = await _transactionService.GetTransactionByIdAsync(transactionId);
            if (transaction == null || transaction.CustomerId != customerId)
                return NotFound("Giao dịch không tồn tại hoặc không thuộc người dùng.");

            if (transaction.Status != BusinessObject.Enums.TransactionStatus.initiated)
                return BadRequest("Giao dịch đã được xử lý hoặc không thể thanh toán.");

            var qrImageUrl = GenerateQrCodeUrl((double)transaction.Amount, transaction.Content ?? $"Thanh toán - {transaction.Id}");

            return Ok(new
            {
                transaction.Id,
                transaction.OrderId,
                transaction.Status,
                transaction.Amount,
                QrImageUrl = qrImageUrl
            });
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
