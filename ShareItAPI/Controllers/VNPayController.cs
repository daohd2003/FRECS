using BusinessObject.DTOs.VNPay;
using BusinessObject.Enums;
using BusinessObject.Enums.VNPay;
using BusinessObject.Models;
using Common.Utilities.VNPAY;
using Common.Utilities.VNPAY.Common.Utilities.VNPAY;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Payments.VNPay;
using Services.Transactions;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/payment/Vnpay")]
    public class VNPayController : ControllerBase
    {
        private readonly IVnpay _vnpay;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VNPayController> _logger;
        private readonly ITransactionService _transactionService;

        public VNPayController(IVnpay vnpay, IConfiguration configuration, ILogger<VNPayController> logger, ITransactionService transactionService)
        {
            _vnpay = vnpay;
            _configuration = configuration;

            _vnpay.Initialize(_configuration["Vnpay:TmnCode"], _configuration["Vnpay:HashSecret"], _configuration["Vnpay:BaseUrl"], _configuration["Vnpay:CallbackUrl"]);
            _logger = logger;
            _transactionService = transactionService;
        }

        /// <summary>
        /// Tạo url thanh toán
        /// </summary>
        /// <param name="money">Số tiền phải thanh toán</param>
        /// <param name="description">Mô tả giao dịch</param>
        /// <returns></returns>
        [HttpGet("CreatePaymentUrl")]
        [Authorize(Roles = "customer")]
        public ActionResult<string> CreatePaymentUrl(Guid orderId, double money, string note)
        {
            var customerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            try
            {
                var ipAddress = NetworkHelper.GetIpAddress(HttpContext);

                var description = $"OID:{orderId}-{note}";

                var request = new PaymentRequest
                {
                    PaymentId = DateTime.Now.Ticks,
                    Money = money,
                    Description = description,
                    IpAddress = ipAddress,
                    BankCode = BankCode.ANY,
                    CreatedDate = DateTime.Now,
                    Currency = Currency.VND,
                    Language = DisplayLanguage.Vietnamese
                };

                var paymentUrl = _vnpay.GetPaymentUrl(request);
                return Created(paymentUrl, paymentUrl);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Thực hiện hành động sau khi thanh toán. URL này cần được khai báo với VNPAY để API này hoạt đồng (ví dụ: http://localhost:1234/api/Vnpay/IpnAction)
        /// </summary>
        /// <returns></returns>
        [HttpGet("IpnAction")]
        [HttpPost("IpnAction")]
        public async Task<IActionResult> IpnAction()
        {
            _logger.LogInformation("IpnAction endpoint was called at {Time}", DateTime.Now);

            if (Request.QueryString.HasValue)
            {
                try
                {
                    var paymentResult = _vnpay.GetPaymentResult(Request.Query);
                    var orderId = GetUIDUtils.ExtractOrderId(paymentResult.Description);
                    if (paymentResult.IsSuccess)
                    {
                        _logger.LogInformation("Payment success for PaymentId: {PaymentId}", paymentResult.PaymentId);

                        // Lấy CustomerId, ProviderId từ order service
                        // var (customerId, providerId) = await _orderService.GetCustomerAndProviderByOrderIdAsync(orderId);

                        // Thực hiện hành động nếu thanh toán thành công tại đây. Ví dụ: Cập nhật trạng thái đơn hàng trong cơ sở dữ liệu.
                        var transaction = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            OrderId = orderId,
                            CustomerId = Guid.Empty,
                            ProviderId = Guid.Empty,
                            Amount = paymentResult.Amount,
                            Status = BusinessObject.Enums.TransactionStatus.completed,
                            PaymentMethod = paymentResult.PaymentMethod,
                            TransactionDate = paymentResult.Timestamp,
                            Content = paymentResult.Description
                        };

                        var transactionJson = System.Text.Json.JsonSerializer.Serialize(transaction);
                        _logger.LogInformation("Transaction to save: {Transaction}", transactionJson);
                        await _transactionService.SaveTransactionAsync(transaction);

                        return Ok();
                    }
                    _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
                    // Thực hiện hành động nếu thanh toán thất bại tại đây. Ví dụ: Hủy đơn hàng.
                    // Lưu transaction thất bại
                    var failedTransaction = new Transaction
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        CustomerId = Guid.Empty,
                        ProviderId = Guid.Empty,
                        Amount = paymentResult.Amount,
                        Status = BusinessObject.Enums.TransactionStatus.failed,
                        PaymentMethod = paymentResult.PaymentMethod,
                        TransactionDate = paymentResult.Timestamp,
                        Content = paymentResult.Description
                    };

                    var failedTransactionJson = System.Text.Json.JsonSerializer.Serialize(failedTransaction);
                    _logger.LogInformation("Failed transaction to save: {Transaction}", failedTransactionJson);

                    await _transactionService.SaveTransactionAsync(failedTransaction);

                    return BadRequest("Thanh toán thất bại");
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }
            _logger.LogWarning("IpnAction called but query string is empty");
            return NotFound("Không tìm thấy thông tin thanh toán.");
        }

        /// <summary>
        /// Trả kết quả thanh toán về cho người dùng
        /// </summary>
        /// <returns></returns>
        [HttpGet("Callback")]
        public ActionResult<PaymentResult> Callback()
        {
            _logger.LogInformation("Callback endpoint was called at {Time}", DateTime.Now);

            if (Request.QueryString.HasValue)
            {
                try
                {
                    var paymentResult = _vnpay.GetPaymentResult(Request.Query);

                    if (paymentResult.IsSuccess)
                    {
                        _logger.LogInformation("Payment success for PaymentId: {PaymentId}", paymentResult.PaymentId);
                        return Ok(paymentResult);
                    }
                    _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
                    return BadRequest(paymentResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Callback");
                    return BadRequest(ex.Message);
                }
            }
            _logger.LogWarning("Callback called but query string is empty");
            return NotFound("Không tìm thấy thông tin thanh toán.");
        }
    }
}
