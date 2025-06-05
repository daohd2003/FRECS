using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.VNPay;
using BusinessObject.Enums;
using BusinessObject.Enums.VNPay;
using BusinessObject.Models;
using Common.Utilities.VNPAY;
using Common.Utilities.VNPAY.Common.Utilities.VNPAY;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.OrderServices;
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
        private readonly IOrderService _orderService;
        private readonly ITransactionService _transactionService;

        public VNPayController(IVnpay vnpay, IConfiguration configuration, ILogger<VNPayController> logger, ITransactionService transactionService, IOrderService orderService)
        {
            _vnpay = vnpay;
            _configuration = configuration;

            _vnpay.Initialize(_configuration["Vnpay:TmnCode"], _configuration["Vnpay:HashSecret"], _configuration["Vnpay:BaseUrl"], _configuration["Vnpay:CallbackUrl"]);
            _logger = logger;
            _transactionService = transactionService;
            _orderService = orderService;
        }

        /// <summary>
        /// Create payment URL
        /// </summary>
        [HttpGet("CreatePaymentUrl")]
        [Authorize(Roles = "customer")]
        public ActionResult<ApiResponse<string>> CreatePaymentUrl(Guid orderId, double money, string note)
        {
            try
            {
                var customerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
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
                return Created("", new ApiResponse<string>("Payment URL created successfully", paymentUrl));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// Handle payment notification callback from VNPay
        /// </summary>
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

                        await _orderService.CompleteTransactionAsync(orderId);

                        return Ok(new ApiResponse<string>("Payment completed successfully", transactionJson));
                    }

                    _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);

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

                    await _orderService.FailTransactionAsync(orderId);

                    return BadRequest(new ApiResponse<string>("Payment failed", failedTransactionJson));
                }
                catch (Exception ex)
                {
                    return BadRequest(new ApiResponse<string>(ex.Message, null));
                }
            }

            _logger.LogWarning("IpnAction called but query string is empty");
            return NotFound(new ApiResponse<string>("Payment information not found.", null));
        }

        /// <summary>
        /// Return payment result to user
        /// </summary>
        [HttpGet("Callback")]
        public ActionResult<ApiResponse<PaymentResult>> Callback()
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
                        return Ok(new ApiResponse<PaymentResult>("Payment succeeded", paymentResult));
                    }

                    _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
                    return BadRequest(new ApiResponse<PaymentResult>("Payment failed", paymentResult));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Callback");
                    return BadRequest(new ApiResponse<string>(ex.Message, null));
                }
            }

            _logger.LogWarning("Callback called but query string is empty");
            return NotFound(new ApiResponse<string>("Payment information not found.", null));
        }
    }
}