using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.VNPay;
using BusinessObject.Enums;
using BusinessObject.Enums.VNPay;
using BusinessObject.Models;
using Common.Utilities.VNPAY;
using Common.Utilities.VNPAY.Common.Utilities.VNPAY;
using DataAccess;
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
        private readonly ShareItDbContext _context;

        public VNPayController(IVnpay vnpay, IConfiguration configuration, ILogger<VNPayController> logger, ITransactionService transactionService, IOrderService orderService, ShareItDbContext context)
        {
            _vnpay = vnpay;
            _configuration = configuration;

            _vnpay.Initialize(_configuration["Vnpay:TmnCode"], _configuration["Vnpay:HashSecret"], _configuration["Vnpay:BaseUrl"], _configuration["Vnpay:CallbackUrl"]);
            _logger = logger;
            _transactionService = transactionService;
            _orderService = orderService;
            _context = context;
        }

        /// <summary>
        /// Create payment URL
        /// </summary>
        [HttpPost("CreatePaymentUrl")]
        [Authorize(Roles = "customer")]
        public async Task<ActionResult<ApiResponse<string>>> CreatePaymentUrl([FromBody] CreatePaymentRequestDto requestDto)
        {
            if (requestDto.OrderIds == null || !requestDto.OrderIds.Any())
            {
                return BadRequest(new ApiResponse<string>("Order IDs are required.", null));
            }

            try
            {
                var customerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                double totalMoney = 0;
                var validOrderIds = new List<Guid>();

                // 1. Lặp qua các orderId để kiểm tra và tính tổng tiền
                foreach (var orderId in requestDto.OrderIds)
                {
                    var order = await _orderService.GetOrderDetailAsync(orderId);
                    if (order == null || order.CustomerId != customerId || order.Status != OrderStatus.pending)
                    {
                        // Bỏ qua các đơn hàng không hợp lệ (không tồn tại, không phải của khách, hoặc đã xử lý)
                        _logger.LogWarning("Invalid or processed order skipped: {OrderId}", orderId);
                        continue;
                    }
                    totalMoney += (double)order.TotalAmount;
                    validOrderIds.Add(orderId);
                }

                if (totalMoney == 0)
                {
                    return BadRequest(new ApiResponse<string>("No valid orders to pay for.", null));
                }

                var ipAddress = NetworkHelper.GetIpAddress(HttpContext);

                // 2. Tạo mô tả thanh toán chứa tất cả các Order ID hợp lệ
                // Ví dụ: "OIDS:guid1,guid2-Thanh toan don hang"
                var orderIdsString = string.Join(",", validOrderIds);
                var description = $"OIDS:{orderIdsString}-{requestDto.Note}";

                var request = new PaymentRequest
                {
                    PaymentId = DateTime.Now.Ticks,
                    Money = totalMoney,
                    Description = description,
                    IpAddress = ipAddress,
                    BankCode = BankCode.ANY,
                    CreatedDate = DateTime.Now,
                    Currency = Currency.VND,
                    Language = DisplayLanguage.Vietnamese
                };

                var paymentUrl = _vnpay.GetPaymentUrl(request);
                return Ok(new ApiResponse<string>("Payment URL created successfully", paymentUrl));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment URL for orders.");
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

            if (!Request.QueryString.HasValue)
            {
                _logger.LogWarning("IpnAction called but query string is empty");
                return NotFound(new ApiResponse<string>("Payment information not found.", null));
            }

            try
            {
                var paymentResult = _vnpay.GetPaymentResult(Request.Query);
                List<Guid> orderIds;

                // 1. Phân tích chuỗi description để lấy danh sách orderId
                string description = paymentResult.Description;
                if (description.StartsWith("OIDS:"))
                {
                    // Tìm vị trí của dấu gạch nối CUỐI CÙNG trong chuỗi
                    int lastHyphenIndex = description.LastIndexOf('-');

                    // Nếu không tìm thấy dấu gạch nối, có thể chuỗi bị lỗi
                    if (lastHyphenIndex == -1)
                    {
                        throw new FormatException("Description string is missing the note separator '-'.");
                    }

                    // Lấy phần chuỗi từ đầu đến vị trí dấu gạch nối cuối cùng
                    // Kết quả: "OIDS:16f51b52-2558-46e2-88b7-40ad65fd9346,2c7243fc-f6c2-42fe-82db-bceea9e73124"
                    string allIdsWithPrefix = description.Substring(0, lastHyphenIndex);

                    // Bỏ đi 5 ký tự "OIDS:" ở đầu
                    // Kết quả: "16f51b52-2558-46e2-88b7-40ad65fd9346,2c7243fc-f6c2-42fe-82db-bceea9e73124"
                    string allIdsString = allIdsWithPrefix.Substring(5);

                    // Tách chuỗi bằng dấu phẩy và parse từng Guid
                    orderIds = allIdsString.Split(',').Select(Guid.Parse).ToList();
                }
                else if (description.StartsWith("OID:")) // Hỗ trợ định dạng cũ nếu cần
                {
                    orderIds = new List<Guid> { GetUIDUtils.ExtractOrderId(description) };
                }
                else
                {
                    throw new Exception("Invalid order description format.");
                }

                // 2. Sử dụng Database Transaction để đảm bảo tất cả các đơn hàng được cập nhật đồng bộ
                using (var dbTransaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        if (paymentResult.IsSuccess)
                        {
                            _logger.LogInformation("Payment success for PaymentId: {PaymentId}, handling Orders: {OrderIds}", paymentResult.PaymentId, string.Join(", ", orderIds));

                            foreach (var orderId in orderIds)
                            {
                                var order = await _orderService.GetOrderDetailAsync(orderId);
                                if (order == null)
                                {
                                    _logger.LogError("Order not found during IPN processing: {OrderId}", orderId);
                                    continue; // Bỏ qua nếu order không tồn tại
                                }

                                // Tạo transaction cho từng order
                                var transaction = new Transaction
                                {
                                    Id = Guid.NewGuid(),
                                    OrderId = orderId,
                                    CustomerId = order.CustomerId,
                                    ProviderId = order.ProviderId,
                                    Amount = order.TotalAmount, // Lấy số tiền của từng order
                                    Status = BusinessObject.Enums.TransactionStatus.completed,
                                    PaymentMethod = paymentResult.PaymentMethod,
                                    TransactionDate = paymentResult.Timestamp,
                                    Content = $"Payment for order {orderId}"
                                };

                                await _transactionService.SaveTransactionAsync(transaction);
                                await _orderService.ChangeOrderStatus(orderId, OrderStatus.approved); // Hoặc trạng thái phù hợp
                            }

                            await dbTransaction.CommitAsync();
                            return Ok(new ApiResponse<string>("All payments completed successfully", null));
                        }
                        else
                        {
                            _logger.LogWarning("Payment failed for PaymentId: {PaymentId}, affecting Orders: {OrderIds}", paymentResult.PaymentId, string.Join(", ", orderIds));
                            foreach (var orderId in orderIds)
                            {
                                await _orderService.FailTransactionAsync(orderId);
                            }
                            await dbTransaction.CommitAsync(); // Commit cả khi fail để ghi lại trạng thái
                            return BadRequest(new ApiResponse<string>("Payment failed", null));
                        }
                    }
                    catch (Exception ex)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogError(ex, "Error processing IPN for orders: {OrderIds}. Transaction rolled back.", string.Join(", ", orderIds));
                        throw; // Ném lại lỗi để hệ thống ghi nhận
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
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