using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.FeedbackServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/feedbacks")]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        private Guid GetCurrentUserId()
        {
            // Try multiple claim types
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                            ?? User.FindFirstValue("sub") 
                            ?? User.FindFirstValue("userId")
                            ?? User.FindFirstValue("id");
            
            Console.WriteLine($"User claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"))}");
            Console.WriteLine($"UserIdString: {userIdString}");
            
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                throw new InvalidOperationException("User ID from authentication token is missing or invalid.");
            }
            return userId;
        }

        /// <summary>
        /// Feature: Give feedback on products
        /// The user writes a review and rates a product they have rented or purchased.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto dto)
        {
            var customerId = GetCurrentUserId();
            var feedbackResponse = await _feedbackService.SubmitFeedbackAsync(dto, customerId);
            // Theo tài liệu API là 201 Created
            return StatusCode(201, new ApiResponse<FeedbackResponseDto>("Feedback submitted successfully.", feedbackResponse));
        }

        // GET - Get all feedback submitted by the current user
        [HttpGet]
        [Authorize(Roles = "customer,provider")]
        public async Task<IActionResult> GetMyFeedbacks()
        {
            var customerId = GetCurrentUserId();
            var feedbacks = await _feedbackService.GetCustomerFeedbacksAsync(customerId);
            return Ok(new ApiResponse<object>("Your feedbacks retrieved successfully.", feedbacks));
        }

        // GET - Get feedback by ID
        [HttpGet("{feedbackId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbackById(Guid feedbackId)
        {
            var feedback = await _feedbackService.GetFeedbackByIdAsync(feedbackId);
            return Ok(new ApiResponse<FeedbackResponseDto>("Feedback retrieved successfully.", feedback));
        }

        /// <summary>
        /// Feature: View feedback
        /// The user views existing customer reviews and ratings for a specific product.
        /// </summary>
        [HttpGet("{targetType}/{targetId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbacksByTarget(string targetType, Guid targetId)
        {
            if (!Enum.TryParse(targetType, true, out FeedbackTargetType type))
            {
                throw new ArgumentException("Invalid target type. Must be 'Product' or 'Order'.");
            }
            var feedbacks = await _feedbackService.GetFeedbacksByTargetAsync(type, targetId);
            return Ok(new ApiResponse<object>("Feedbacks retrieved successfully.", feedbacks));
        }

        // GET - Get all feedback for products/orders owned by a provider
        [HttpGet("owned-by-provider/{providerId:guid}")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> GetFeedbacksByProviderIdAsync(Guid providerId)
        {
            var currentUserId = GetCurrentUserId();
            bool isAdmin = User.IsInRole("admin");
            var feedbacks = await _feedbackService.GetFeedbacksByProviderIdAsync(providerId, currentUserId, isAdmin);
            return Ok(new ApiResponse<object>("Owned feedbacks retrieved successfully.", feedbacks));
        }

        // PUT - Update feedback
        [HttpPut("{feedbackId:guid}")]
        [Authorize(Roles = "customer,provider,admin")]
        public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] FeedbackRequestDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _feedbackService.UpdateFeedbackAsync(feedbackId, dto, currentUserId);
            return Ok(new ApiResponse<string>("Feedback updated successfully.", null));
        }

        // DELETE /api/feedbacks/{feedbackId} - Delete feedback
        [HttpDelete("{feedbackId:guid}")]
        [Authorize(Roles = "customer,provider,admin")]
        public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
        {
            var currentUserId = GetCurrentUserId();
            await _feedbackService.DeleteFeedbackAsync(feedbackId, currentUserId);
            return Ok(new ApiResponse<string>("Feedback deleted successfully.", null));
        }

        // PUT /api/feedbacks/{feedbackId}/response - Submit provider response
        [HttpPut("{feedbackId:guid}/response")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> SubmitProviderResponse(Guid feedbackId, [FromBody] SubmitProviderResponseDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _feedbackService.SubmitProviderResponseAsync(feedbackId, dto, currentUserId);
            return Ok(new ApiResponse<string>("Provider response submitted successfully.", null));
        }

        /// <summary>
        /// Update provider response to existing feedback
        /// PUT: api/feedback/{feedbackId}/response/update
        /// </summary>
        [HttpPut("{feedbackId:guid}/response/update")]
        [Authorize(Roles = "provider,admin")]
        public async Task<IActionResult> UpdateProviderResponse(Guid feedbackId, [FromBody] UpdateProviderResponseDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _feedbackService.UpdateProviderResponseAsync(feedbackId, dto, currentUserId);
                return Ok(new ApiResponse<string>("Provider response updated successfully.", null));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Update customer feedback (rating and comment)
        /// PUT: api/feedback/{feedbackId}/update
        /// </summary>
        [HttpPut("{feedbackId:guid}/update")]
        [Authorize(Roles = "customer,admin")]
        public async Task<IActionResult> UpdateCustomerFeedback(Guid feedbackId, [FromBody] UpdateFeedbackDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, dto, currentUserId);
                return Ok(new ApiResponse<string>("Feedback updated successfully.", null));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"An error occurred: {ex.Message}", null));
            }
        }
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetFeedbacksByProduct(Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        {
            // Get current user ID if authenticated
            Guid? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    currentUserId = GetCurrentUserId();
                    Console.WriteLine($"[GET FEEDBACKS] Authenticated user: {currentUserId}");
                }
                catch
                {
                    // User not authenticated, continue as anonymous
                    Console.WriteLine($"[GET FEEDBACKS] Failed to get user ID from token");
                }
            }
            else
            {
                Console.WriteLine($"[GET FEEDBACKS] Anonymous request - no authentication");
            }
            
            Console.WriteLine($"[GET FEEDBACKS] ProductId: {productId}, CurrentUserId: {currentUserId?.ToString() ?? "null"}");
            var response = await _feedbackService.GetFeedbacksByProductAsync(productId, page, pageSize, currentUserId);
            if (response.Data == null)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Lấy tất cả feedback của một customer cụ thể cho một sản phẩm
        /// Dùng cho Provider xem feedback của customer đã mua sản phẩm trong Order Detail
        /// </summary>
        [HttpGet("product/{productId}/customer/{customerId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbacksByProductAndCustomer(Guid productId, Guid customerId)
        {
            var feedbacks = await _feedbackService.GetFeedbacksByProductAndCustomerAsync(productId, customerId);
            return Ok(new ApiResponse<object>("Feedbacks retrieved successfully.", feedbacks));
        }
        // ===== FEEDBACK MANAGEMENT ENDPOINTS (Staff/Admin) =====
        
        // GET - Get all feedbacks with filters (Staff/Admin)
        [HttpGet("management")]
         [Authorize(Roles = "staff,admin")] // TODO: Uncomment sau khi test xong
        public async Task<IActionResult> GetAllFeedbacks([FromQuery] FeedbackFilterDto filter)
        {
            var response = await _feedbackService.GetAllFeedbacksAsync(filter);
            if (response.Data == null)
                return BadRequest(response);
            return Ok(response);
        }

        // GET - Get feedback detail (Staff/Admin)
        [HttpGet("management/{feedbackId}")]
         [Authorize(Roles = "staff,admin")] // TODO: Uncomment sau khi test xong
        public async Task<IActionResult> GetFeedbackDetail(Guid feedbackId)
        {
            var response = await _feedbackService.GetFeedbackDetailAsync(feedbackId);
            if (response.Data == null)
                return NotFound(response);
            return Ok(response);
        }

        // PUT - Block feedback (Staff/Admin)
        [HttpPut("management/{feedbackId}/block")]
         [Authorize(Roles = "staff,admin")] // TODO: Uncomment sau khi test xong
        public async Task<IActionResult> BlockFeedback(Guid feedbackId)
        {
            try
            {
                Console.WriteLine($"BlockFeedback called with feedbackId: {feedbackId}");
                var staffId = GetCurrentUserId();
                Console.WriteLine($"StaffId: {staffId}");
                
                var response = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);
                Console.WriteLine($"Service response - Success: {response.Data}, Message: {response.Message}");
                
                if (!response.Data)
                    return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BlockFeedback: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // PUT - Unblock feedback (Staff/Admin)
        [HttpPut("management/{feedbackId}/unblock")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> UnblockFeedback(Guid feedbackId)
        {
            var response = await _feedbackService.UnblockFeedbackAsync(feedbackId);
            if (!response.Data)
                return BadRequest(response);
            return Ok(response);
        }

        // TEST - Test content moderation
        [HttpPost("test-moderation")]
        public async Task<IActionResult> TestModeration([FromBody] string content)
        {
            try
            {
                var moderationService = HttpContext.RequestServices.GetService<Services.ContentModeration.IContentModerationService>();
                if (moderationService == null)
                {
                    return Ok(new { error = "ContentModerationService not registered" });
                }
                
                var result = await moderationService.CheckContentAsync(content);
                return Ok(new { 
                    content = content,
                    isViolation = !result.IsAppropriate, 
                    reason = result.Reason,
                    violatedTerms = result.ViolatedTerms,
                    message = "Moderation check completed"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
        
        // GET - Get all feedbacks for a product (Staff/Admin - no filtering)
        [HttpGet("management/product/{productId}")]
         [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetAllFeedbacksForProduct(Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            var response = await _feedbackService.GetAllFeedbacksByProductForStaffAsync(productId, page, pageSize);
            if (response.Data == null)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
        
        // GET - Get feedback statistics (Staff/Admin)
        [HttpGet("management/statistics")]
         [Authorize(Roles = "staff,admin")] // TODO: Uncomment sau khi test xong
        public async Task<IActionResult> GetFeedbackStatistics()
        {
            var response = await _feedbackService.GetFeedbackStatisticsAsync();
            if (response.Data == null)
                return BadRequest(response);
            return Ok(response);
        }
    }
}
