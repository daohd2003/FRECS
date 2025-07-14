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
using Microsoft.EntityFrameworkCore;
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
                decimal totalMoney = 0;
                var validOrders = new List<BusinessObject.Models.Order>();

                // 1. Lặp qua các orderId để lấy object và tính tổng tiền
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
                {
                    return BadRequest(new ApiResponse<string>("No valid orders to pay for.", null));
                }

                // 2. TẠO MỘT TRANSACTION DUY NHẤT ĐỂ NHÓM CÁC ĐƠN HÀNG
                var transaction = new BusinessObject.Models.Transaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    Amount = totalMoney,
                    Status = BusinessObject.Enums.TransactionStatus.initiated,
                    TransactionDate = DateTime.UtcNow,
                    Orders = validOrders,
                    PaymentMethod = "VNPAY",
                    Content = requestDto.Note
                };

                // 3. Lưu transaction mới này vào DB (bạn cần có service cho việc này)
                await _transactionService.SaveTransactionAsync(transaction);

                var ipAddress = NetworkHelper.GetIpAddress(HttpContext);

                // 4. SỬ DỤNG ID CỦA TRANSACTION TỔNG làm nội dung thanh toán
                var description = $"TID:{transaction.Id}";

                var request = new PaymentRequest
                {
                    PaymentId = DateTime.Now.Ticks,
                    Money = (double) totalMoney,
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
                _logger.LogError(ex, "Error creating VNPay payment URL.");
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
            _logger.LogInformation("VNPay IPN endpoint was called at {Time}", DateTime.Now);

            if (!Request.QueryString.HasValue)
            {
                return NotFound(); // Trả về mã lỗi chuẩn của VNPay
            }

            try
            {
                var paymentResult = _vnpay.GetPaymentResult(Request.Query);
                Guid transactionId;

                // 1. Phân tích chuỗi description để lấy transactionId duy nhất
                string description = paymentResult.Description;
                if (description != null && description.StartsWith("TID:"))
                {
                    transactionId = Guid.Parse(description.Substring(4));
                }
                else
                {
                    _logger.LogError("Invalid description format in IPN: {Description}", description);
                    return BadRequest(new { RspCode = "01", Message = "Order not found" });
                }

                // 2. Sử dụng Database Transaction để đảm bảo tính toàn vẹn
                using (var dbTransaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Tìm transaction tổng và các order liên quan
                        var transaction = await _context.Transactions
                                                        .Include(t => t.Orders) // Nạp các Order liên quan
                                                        .FirstOrDefaultAsync(t => t.Id == transactionId);

                        if (transaction == null)
                        {
                            _logger.LogError("Transaction not found for IPN: {TransactionId}", transactionId);
                            return Ok(new { RspCode = "01", Message = "Order not found" });
                        }

                        // Kiểm tra xem giao dịch đã được xử lý chưa
                        if (transaction.Status != BusinessObject.Enums.TransactionStatus.initiated)
                        {
                            _logger.LogWarning("Transaction {TransactionId} already processed.", transactionId);
                            return Ok(new { RspCode = "00", Message = "Confirm Success" }); // Báo thành công vì đã xử lý rồi
                        }

                        if (paymentResult.IsSuccess)
                        {
                            _logger.LogInformation("Payment success for Transaction: {TransactionId}", transactionId);

                            // Cập nhật transaction tổng
                            transaction.Status = BusinessObject.Enums.TransactionStatus.completed;

                            // Cập nhật tất cả các order con
                            foreach (var order in transaction.Orders)
                            {
                                await _orderService.ChangeOrderStatus(order.Id, OrderStatus.approved);

                                await _orderService.ClearCartItemsForOrderAsync(order);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Payment failed for Transaction: {TransactionId}", transactionId);
                            transaction.Status = BusinessObject.Enums.TransactionStatus.failed;
                            // Cập nhật trạng thái thất bại cho các order con
                            foreach (var order in transaction.Orders)
                            {
                                await _orderService.FailTransactionAsync(order.Id);
                            }
                        }

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        return Ok(new { RspCode = "00", Message = "Confirm Success" });
                    }
                    catch (Exception ex)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogError(ex, "Error processing IPN for Transaction: {TransactionId}. Rolled back.", transactionId);
                        // Khi có lỗi phía server, không nên báo thành công cho VNPay
                        return BadRequest(new { RspCode = "99", Message = "Input data required" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General error in IpnAction.");
                return BadRequest(new { RspCode = "99", Message = "Input data required" });
            }
        }
        /// <summary>
        /// Return payment result to user
        /// </summary>
        [HttpGet("Callback")]
        public IActionResult Callback() // Thay đổi kiểu trả về thành IActionResult
        {
            _logger.LogInformation("Callback endpoint was called at {Time}", DateTime.Now);

            // Lấy URL frontend từ cấu hình
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"];
            if (string.IsNullOrEmpty(frontendBaseUrl))
            {
                _logger.LogError("Frontend:BaseUrl is not configured.");
                return BadRequest(new { RspCode = "99", Message = "Frontend base URL not configured." });
            }

            if (Request.QueryString.HasValue)
            {
                try
                {
                    var paymentResult = _vnpay.GetPaymentResult(Request.Query);

                    if (paymentResult.IsSuccess)
                    {
                        _logger.LogInformation("Payment success for PaymentId: {PaymentId}", paymentResult.PaymentId);
                        // Chuyển hướng về trang chủ hoặc trang xác nhận thanh toán thành công của bạn
                        // Bạn có thể truyền các tham số qua URL để frontend xử lý (ví dụ: trạng thái, transactionId)
                        return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1&paymentStatus=success&vnp_TxnRef={paymentResult.PaymentId}");
                    }
                    else
                    {
                        _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
                        // Chuyển hướng về trang lỗi thanh toán của bạn
                        return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1&paymentStatus=failed&vnp_TxnRef={paymentResult.PaymentId}&vnp_Message={paymentResult.PaymentResponse?.Description ?? "Lỗi không xác định"}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Callback");
                    // Chuyển hướng về trang lỗi chung hoặc trang ban đầu
                    return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1&paymentStatus=error&errorMessage={Uri.EscapeDataString(ex.Message)}");
                }
            }

            _logger.LogWarning("Callback called but query string is empty");
            // Nếu không có query string, cũng chuyển hướng về một trang với thông báo lỗi
            return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1&paymentStatus=error&errorMessage={Uri.EscapeDataString("Payment information not found.")}");
        }
        ///// <summary>
        ///// Return payment result to user
        ///// </summary>
        //[HttpGet("Callback")]
        //public ActionResult<ApiResponse<PaymentResult>> Callback()
        //{
        //    _logger.LogInformation("Callback endpoint was called at {Time}", DateTime.Now);

        //    if (Request.QueryString.HasValue)
        //    {
        //        try
        //        {
        //            var paymentResult = _vnpay.GetPaymentResult(Request.Query);

        //            if (paymentResult.IsSuccess)
        //            {
        //                _logger.LogInformation("Payment success for PaymentId: {PaymentId}", paymentResult.PaymentId);
        //                return Ok(new ApiResponse<PaymentResult>("Payment succeeded", paymentResult));
        //            }

        //            _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
        //            return BadRequest(new ApiResponse<PaymentResult>("Payment failed", paymentResult));
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error processing Callback");
        //            return BadRequest(new ApiResponse<string>(ex.Message, null));
        //        }
        //    }

        //    _logger.LogWarning("Callback called but query string is empty");
        //    return NotFound(new ApiResponse<string>("Payment information not found.", null));
        //}
    }
}