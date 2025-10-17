using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.RentalViolationServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/rental-violations")]
    [Authorize]
    public class RentalViolationController : ControllerBase
    {
        private readonly IRentalViolationService _violationService;

        public RentalViolationController(IRentalViolationService violationService)
        {
            _violationService = violationService;
        }

        /// <summary>
        /// [PROVIDER] Tạo vi phạm mới cho đơn hàng - ViolationId sẽ tự động được tạo
        /// </summary>
        /// <remarks>
        /// Endpoint này dùng để TẠO MỚI vi phạm. ViolationId sẽ được hệ thống tự động generate.
        /// 
        /// Provider chỉ cần cung cấp:
        /// - OrderId: ID của đơn hàng
        /// - Violations: Danh sách các vi phạm (mỗi vi phạm bao gồm OrderItemId, ViolationType, Description, PenaltyAmount, EvidenceFiles)
        /// 
        /// Hệ thống sẽ trả về danh sách ViolationId đã được tạo.
        /// </remarks>
        [HttpPost]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> CreateMultipleViolations([FromForm] CreateMultipleViolationsRequestDto dto)
        {
            try
            {
                var providerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(providerIdClaim) || !Guid.TryParse(providerIdClaim, out Guid providerId))
                {
                    return Unauthorized(new ApiResponse<string>("Không xác định được provider", null));
                }

                var violationIds = await _violationService.CreateMultipleViolationsAsync(dto, providerId);

                return Ok(new ApiResponse<List<Guid>>(
                    $"Đã tạo {violationIds.Count} vi phạm thành công",
                    violationIds
                ));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [ALL] Lấy chi tiết một vi phạm đã tồn tại
        /// </summary>
        /// <param name="violationId">ID của vi phạm cần xem chi tiết</param>
        [HttpGet("{violationId:guid}")]
        public async Task<IActionResult> GetViolationDetail(Guid violationId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                {
                    return Unauthorized();
                }

                if (!Enum.TryParse<UserRole>(roleClaim, out UserRole userRole))
                {
                    return Unauthorized();
                }

                // Check access permission
                var canAccess = await _violationService.CanUserAccessViolationAsync(violationId, userId, userRole);
                if (!canAccess)
                {
                    return Forbid();
                }

                var detail = await _violationService.GetViolationDetailAsync(violationId);
                if (detail == null)
                {
                    return NotFound(new ApiResponse<string>("Vi phạm không tồn tại", null));
                }

                return Ok(new ApiResponse<RentalViolationDetailDto>("Lấy chi tiết thành công", detail));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [ALL] Lấy danh sách tất cả vi phạm của một đơn hàng
        /// </summary>
        /// <param name="orderId">ID của đơn hàng</param>
        [HttpGet("order/{orderId:guid}")]
        public async Task<IActionResult> GetViolationsByOrder(Guid orderId)
        {
            try
            {
                var violations = await _violationService.GetViolationsByOrderIdAsync(orderId);
                return Ok(new ApiResponse<IEnumerable<RentalViolationDto>>(
                    "Lấy danh sách vi phạm thành công",
                    violations
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [PROVIDER] Điều chỉnh vi phạm sau khi Customer từ chối
        /// </summary>
        /// <remarks>
        /// Chỉ có thể cập nhật khi trạng thái vi phạm là CUSTOMER_REJECTED.
        /// Provider có thể điều chỉnh: Description, PenaltyPercentage, PenaltyAmount
        /// Sau khi cập nhật, status sẽ quay về PENDING để Customer xem xét lại.
        /// </remarks>
        /// <param name="violationId">ID của vi phạm cần cập nhật</param>
        [HttpPut("{violationId:guid}")]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> UpdateViolation(Guid violationId, [FromBody] UpdateViolationDto dto)
        {
            try
            {
                var providerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(providerIdClaim) || !Guid.TryParse(providerIdClaim, out Guid providerId))
                {
                    return Unauthorized();
                }

                var result = await _violationService.UpdateViolationByProviderAsync(violationId, dto, providerId);
                if (!result)
                {
                    return NotFound(new ApiResponse<string>("Vi phạm không tồn tại", null));
                }

                return Ok(new ApiResponse<string>("Cập nhật vi phạm thành công", null));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [CUSTOMER] Phản hồi một vi phạm đã tồn tại (đồng ý hoặc từ chối)
        /// </summary>
        /// <remarks>
        /// Endpoint này dùng để PHẢN HỒI một vi phạm đã được Provider tạo trước đó.
        /// 
        /// Customer cần:
        /// - Cung cấp ViolationId trong URL (ID của vi phạm đã tồn tại)
        /// - IsAccepted: true = đồng ý bồi thường, false = từ chối
        /// - CustomerNotes: Lý do từ chối (bắt buộc nếu IsAccepted = false)
        /// - EvidenceFiles: Ảnh/video phản biện (optional)
        /// </remarks>
        /// <param name="violationId">ID của vi phạm cần phản hồi (lấy từ danh sách vi phạm)</param>
        [HttpPost("{violationId:guid}/respond")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> CustomerRespond(Guid violationId, [FromForm] CustomerViolationResponseDto dto)
        {
            try
            {
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out Guid customerId))
                {
                    return Unauthorized();
                }

                var result = await _violationService.CustomerRespondToViolationAsync(violationId, dto, customerId);
                if (!result)
                {
                    return NotFound(new ApiResponse<string>("Vi phạm không tồn tại", null));
                }

                return Ok(new ApiResponse<string>(
                    dto.IsAccepted ? "Đã chấp nhận yêu cầu bồi thường" : "Đã gửi phản hồi",
                    null
                ));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [CUSTOMER] Lấy danh sách tất cả vi phạm của tôi
        /// </summary>
        [HttpGet("customer/my-violations")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> GetMyViolations()
        {
            try
            {
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out Guid customerId))
                {
                    return Unauthorized();
                }

                var violations = await _violationService.GetCustomerViolationsAsync(customerId);
                return Ok(new ApiResponse<IEnumerable<RentalViolationDto>>(
                    "Lấy danh sách vi phạm thành công",
                    violations
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }

        /// <summary>
        /// [PROVIDER] Lấy danh sách tất cả vi phạm tôi đã tạo
        /// </summary>
        [HttpGet("provider/my-violations")]
        [Authorize(Roles = "provider")]
        public async Task<IActionResult> GetProviderViolations()
        {
            try
            {
                var providerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(providerIdClaim) || !Guid.TryParse(providerIdClaim, out Guid providerId))
                {
                    return Unauthorized();
                }

                var violations = await _violationService.GetProviderViolationsAsync(providerId);
                return Ok(new ApiResponse<IEnumerable<RentalViolationDto>>(
                    "Lấy danh sách vi phạm thành công",
                    violations
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
        }
    }
}