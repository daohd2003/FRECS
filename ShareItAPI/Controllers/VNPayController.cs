using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.VNPay;
using BusinessObject.Enums;
using BusinessObject.Enums.VNPay;
using BusinessObject.Models;
using BusinessObject.Utilities;
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
using Services.CartServices;
using System.Security.Claims;
using ShareItAPI.Extensions;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/payment/Vnpay")]
    public class VNPayController : ControllerBase
    {
        private readonly IVnpay _vnpay;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<VNPayController> _logger;
        private readonly IOrderService _orderService;
        private readonly ITransactionService _transactionService;
        private readonly ICartService _cartService;
        private readonly ShareItDbContext _context;

        public VNPayController(IVnpay vnpay, IConfiguration configuration, IWebHostEnvironment environment, ILogger<VNPayController> logger, ITransactionService transactionService, IOrderService orderService, ICartService cartService, ShareItDbContext context)
        {
            _vnpay = vnpay;
            _configuration = configuration;
            _environment = environment;

            _vnpay.Initialize(
                _configuration["Vnpay:TmnCode"], 
                _configuration["Vnpay:HashSecret"], 
                _configuration["Vnpay:BaseUrl"], 
                _configuration.GetVnpayCallbackUrl(_environment)
            );
            _logger = logger;
            _transactionService = transactionService;
            _orderService = orderService;
            _cartService = cartService;
            _context = context;
        }

        /// <summary>
        /// Create payment URL
        /// </summary>
        [HttpPost("CreatePaymentUrl")]
        [Authorize(Roles = "customer,provider")]
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
                    TransactionDate = DateTimeHelper.GetVietnamTime(),
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
                                    
                                    // Record discount code usage if order has discount code
                                    if (order.DiscountCodeId.HasValue)
                                    {
                                        try
                                        {
                                            var usedDiscountCode = new UsedDiscountCode
                                            {
                                                Id = Guid.NewGuid(),
                                                UserId = order.CustomerId,
                                                DiscountCodeId = order.DiscountCodeId.Value,
                                                OrderId = order.Id,
                                                UsedAt = DateTimeHelper.GetVietnamTime()
                                            };
                                            await _context.UsedDiscountCodes.AddAsync(usedDiscountCode);
                                            
                                            // Increment the used count for the discount code
                                            var discountCode = await _context.DiscountCodes.FindAsync(order.DiscountCodeId.Value);
                                            if (discountCode != null)
                                            {
                                                discountCode.UsedCount++;
                                                _context.DiscountCodes.Update(discountCode);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Failed to record discount code usage for Order {OrderId}", order.Id);
                                            // Don't fail the whole transaction if discount recording fails
                                        }
                                    }
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
        public async Task<IActionResult> Callback() // Async method to support cart clearing
        {
            _logger.LogInformation("Callback endpoint was called at {Time}", DateTime.Now);

            // Lấy URL frontend từ cấu hình
            var frontendBaseUrl = _configuration.GetFrontendBaseUrl(_environment);
            
            // Fallback to default if not configured
            if (string.IsNullOrEmpty(frontendBaseUrl))
            {
                _logger.LogError("FrontendSettings:{Environment}:BaseUrl is not configured. Using default.", _environment.EnvironmentName);
                frontendBaseUrl = _environment.IsDevelopment() 
                    ? "https://localhost:7045" 
                    : "https://share-it-fe-ewh8dbeahgaagufb.southeastasia-01.azurewebsites.net";
            }

            if (Request.QueryString.HasValue)
            {
                try
                {
                    var paymentResult = _vnpay.GetPaymentResult(Request.Query);

                    if (paymentResult.IsSuccess)
                    {
                        _logger.LogInformation("Payment success for PaymentId: {PaymentId}", paymentResult.PaymentId);
                        // Chuyển hướng về order history với success status
                        return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1&paymentStatus=success&vnp_TxnRef={paymentResult.PaymentId}");
                    }
                    else
                    {
                        _logger.LogWarning("Payment failed for PaymentId: {PaymentId}", paymentResult.PaymentId);
                        
                        // Clear cart when payment failed/cancelled
                        try
                        {
                            string description = paymentResult.Description;
                            if (description != null && description.StartsWith("TID:"))
                            {
                                var transactionId = Guid.Parse(description.Substring(4));
                                var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId);
                                
                                if (transaction != null)
                                {
                                    await _cartService.ClearCartAsync(transaction.CustomerId);
                                    _logger.LogInformation("Cart cleared for CustomerId: {CustomerId} after payment cancellation", transaction.CustomerId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error clearing cart after payment failure");
                            // Continue redirect even if cart clear fails
                        }
                        
                        // Chuyển hướng về order history với failed status
                        return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Callback");
                    // Vẫn redirect về order history ngay cả khi có lỗi
                    return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1");
                }
            }

            _logger.LogWarning("Callback called but query string is empty");
            // Luôn redirect về order history
            return Redirect($"{frontendBaseUrl}/Profile?tab=orders&page=1");
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