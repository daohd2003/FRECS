using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.FeedbackServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/feedbacks")]
    [Authorize(Roles = "customer,admin,provider")]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                throw new UnauthorizedAccessException("User ID not found or invalid.");
            }
            return userId;
        }

        // POST /api/feedbacks - Submit feedback for a product or order
        [HttpPost]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto dto)
        {
            try
            {
                var customerId = GetCurrentUserId();
                var feedbackResponse = await _feedbackService.SubmitFeedbackAsync(dto, customerId);
                return Ok(new ApiResponse<FeedbackResponseDto>("Feedback submitted successfully.", feedbackResponse));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<string>(ex.Message, null)); // Đổi Forbid -> StatusCode(403)
            }
            catch (Exception ex)
            {
                // Log lỗi
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // GET /api/feedbacks - Get all feedback submitted by the current user
        [HttpGet]
        public async Task<IActionResult> GetMyFeedbacks()
        {
            try
            {
                var customerId = GetCurrentUserId();
                var feedbacks = await _feedbackService.GetCustomerFeedbacksAsync(customerId);
                return Ok(new ApiResponse<object>("Your feedbacks retrieved successfully.", feedbacks));
            }
            catch (Exception ex)
            {
                // Log lỗi
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // GET /api/feedbacks/{feedbackId} - Get feedback by ID
        [HttpGet("{feedbackId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbackById(Guid feedbackId)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackByIdAsync(feedbackId);
                return Ok(new ApiResponse<FeedbackResponseDto>("Feedback retrieved successfully.", feedback));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // GET /api/feedbacks/target/{targetType}/{targetId} - Get all feedback for a specific product or order
        [HttpGet("target/{targetType}/{targetId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeedbacksByTarget(string targetType, Guid targetId)
        {
            try
            {
                if (!Enum.TryParse(targetType, true, out FeedbackTargetType type))
                {
                    return BadRequest(new ApiResponse<string>("Invalid target type. Must be 'Product' or 'Order'.", null));
                }

                var feedbacks = await _feedbackService.GetFeedbacksByTargetAsync(type, targetId);
                return Ok(new ApiResponse<object>("Feedbacks retrieved successfully.", feedbacks));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }
        // GET /api/feedbacks/owned-by-provider/{providerId}
        [HttpGet("owned-by-provider/{providerId:guid}")]
        [Authorize(Roles = "provider,admin")] 
        public async Task<IActionResult> GetOwnedFeedbacksByProvider(Guid providerId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                bool isAdmin = User.IsInRole("admin");

                // Gọi hàm Service mới
                var feedbacks = await _feedbackService.GetFeedbacksByProviderIdAsync(providerId, currentUserId, isAdmin);
                return Ok(new ApiResponse<object>("Owned feedbacks retrieved successfully.", feedbacks));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                // Log lỗi
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // PUT /api/feedbacks/{feedbackId} - Update feedback
        [HttpPut("{feedbackId:guid}")]
        [Authorize(Roles = "customer,admin")] // Chỉ customer/admin được cập nhật feedback của mình
        public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] FeedbackRequestDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _feedbackService.UpdateFeedbackAsync(feedbackId, dto, currentUserId);
                return Ok(new ApiResponse<string>("Feedback updated successfully.", null));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<string>(ex.Message, null));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // DELETE /api/feedbacks/{feedbackId} - Delete feedback
        [HttpDelete("{feedbackId:guid}")]
        [Authorize(Roles = "customer,admin")] 
        public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _feedbackService.DeleteFeedbackAsync(feedbackId, currentUserId);
                return Ok(new ApiResponse<string>("Feedback deleted successfully.", null));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }

        // PUT /api/feedbacks/{feedbackId}/response
        [HttpPut("{feedbackId:guid}/response")]
        [Authorize(Roles = "provider,admin")] 
        public async Task<IActionResult> SubmitProviderResponse(Guid feedbackId, [FromBody] SubmitProviderResponseDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _feedbackService.SubmitProviderResponseAsync(feedbackId, dto, currentUserId);
                return Ok(new ApiResponse<string>("Provider response submitted successfully.", null));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>(ex.Message, null));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<string>(ex.Message, null));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>("An unexpected error occurred. Please try again later.", null));
            }
        }
    }
}
