using Azure.Core;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.BankQR;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Enums;
using BusinessObject.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QRCoder;
using Services.OrderServices;
using Services.ProfileServices;
using Services.Transactions;
using Services.UserServices;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;

namespace ShareItAPI.Controllers
{
    [Route("api/transactions")]
    [ApiController]
    [Authorize(Roles = "customer,provider")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly BankQrConfig _bankQrConfig;
        private readonly IUserService _userService;
        private readonly IProfileService _profileService;
        private readonly IOrderService _orderService;

        public TransactionController(ITransactionService transactionService, IOptions<BankQrConfig> bankQrOptions, IUserService userService, IProfileService profileService, IOrderService orderService)
        {
            _transactionService = transactionService;
            _userService = userService;
            _profileService = profileService;
            _bankQrConfig = bankQrOptions.Value;
            _orderService = orderService;
        }

        /// <summary>
        /// Feature: Track spending
        /// The user reviews reports and summaries of financial transactions to monitor their spending.
        /// </summary>
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
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest requestDto)
        {
            if (requestDto.OrderIds == null || !requestDto.OrderIds.Any())
                return BadRequest(new ApiResponse<object>("Order IDs are required.", null));

            var customerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            decimal totalMoney = 0;
            var validOrders = new List<BusinessObject.Models.Order>();

            // 1. Lấy và xác thực các đơn hàng
            foreach (var orderId in requestDto.OrderIds)
            {
                var order = await _orderService.GetOrderEntityByIdAsync(orderId);
                if (order != null && order.CustomerId == customerId && order.Status == OrderStatus.pending)
                {
                    totalMoney += order.TotalAmount;
                    validOrders.Add(order);
                }
            }

            if (!validOrders.Any())
                return BadRequest(new ApiResponse<object>("No valid orders found.", null));

            // 2. Tạo một bản ghi Transaction DUY NHẤT để nhóm các đơn hàng
            var transaction = new BusinessObject.Models.Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                Amount = totalMoney,
                Status = BusinessObject.Enums.TransactionStatus.initiated,
                TransactionDate = DateTimeHelper.GetVietnamTime(),
                Orders = validOrders,
                PaymentMethod = "SEPay"
            };

            // 3. Lưu transaction mới này vào DB
            await _transactionService.SaveTransactionAsync(transaction);

            // 4. Sử dụng ID của transaction VỪA TẠO để nhúng vào QR
            var hiddenDescription = $"TID {transaction.Id}";

            var qrImageUrl = GenerateQrCodeUrl((double)transaction.Amount, hiddenDescription);

            return Ok(new ApiResponse<object>("QR created successfully", new
            {
                amount = transaction.Amount,
                transactionId = transaction.Id,
                orderIds = validOrders.Select(o => o.Id),
                qrImageUrl
            }));
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
                transaction.Status,
                transaction.Amount,
                QrImageUrl = qrImageUrl
            };

            return Ok(new ApiResponse<object>("Payment QR code generated successfully", responseData));
        }

        private async Task<string> GenerateQrCodeBase64FromUrl(double amount, string description)
        {
            string url = GenerateQrCodeUrl(amount, description);

            using var httpClient = new HttpClient();
            byte[] imageBytes = await httpClient.GetByteArrayAsync(url);

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
        [HttpGet("{transactionId}/status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTransactionStatus(Guid transactionId)
        {
            try
            {
                Console.WriteLine($"Attempting to fetch transaction with ID: {transactionId}");
                var transaction = await _transactionService.GetTransactionByIdAsync(transactionId);
                if (transaction == null)
                {
                    Console.WriteLine($"Transaction with ID {transactionId} not found.");
                    return NotFound(new ApiResponse<object>("Transaction not found.", null));
                }
                Console.WriteLine($"Transaction found. Status: {transaction.Status}");
                return Ok(new ApiResponse<object>("Success", new { status = transaction.Status.ToString() }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTransactionStatus for transaction {transactionId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new ApiResponse<object>(ex.Message, null));
            }
        }
    }
}