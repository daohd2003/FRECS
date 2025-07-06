using Azure.Core;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.BankQR;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Enums;
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
    [Authorize(Roles = "customer")]
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
            double totalMoney = 0;
            var validOrderIds = new List<Guid>();
            var profile = await _profileService.GetByUserIdAsync(customerId);

            foreach (var orderId in requestDto.OrderIds)
            {
                var order = await _orderService.GetOrderDetailAsync(orderId);
                if (order != null && order.CustomerId == customerId && order.Status == OrderStatus.pending)
                {
                    totalMoney += (double)order.TotalAmount;
                    validOrderIds.Add(orderId);
                }
            }

            if (totalMoney == 0)
                return BadRequest(new ApiResponse<object>("No valid orders found.", null));

            // Mô tả kiểu giống VNPay
            var orderIdsString = string.Join(" ", validOrderIds);
            var fullName = string.IsNullOrWhiteSpace(profile.FullName) ? "Customer" : profile.FullName.Trim();
            // Description gọn để hiển thị trong QR
            var displayDescription = $"{fullName} chuyen tien";

            // Full description ẩn chứa orderIds, webhook sẽ dùng cái này để xử lý
            var hiddenDescription = $"OIDS {orderIdsString}";

            var QrImage = GenerateQrCodeUrl(totalMoney, hiddenDescription);

            return Ok(new ApiResponse<object>("QR created successfully", new
            {
                amount = totalMoney,
                orderIds = validOrderIds,
                hiddenDescription,
                QrImage
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
                transaction.OrderId,
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
    }
}