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
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                throw new InvalidOperationException("User ID from authentication token is missing or invalid.");
            }
            return userId;
        }

        // POST- Submit feedback for a product or order
        [HttpPost]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto dto)
        {
            var customerId = GetCurrentUserId();
            var feedbackResponse = await _feedbackService.SubmitFeedbackAsync(dto, customerId);
            // Theo tài liệu API là 201 Created
            return StatusCode(201, new ApiResponse<FeedbackResponseDto>("Feedback submitted successfully.", feedbackResponse));
        }

        // GET - Get all feedback submitted by the current user
        [HttpGet]
        [Authorize(Roles = "customer")] 
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

        // GET - Get all feedback for a specific product or order
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
        [Authorize(Roles = "customer,admin")]
        public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] FeedbackRequestDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _feedbackService.UpdateFeedbackAsync(feedbackId, dto, currentUserId);
            return Ok(new ApiResponse<string>("Feedback updated successfully.", null));
        }

        // DELETE /api/feedbacks/{feedbackId} - Delete feedback
        [HttpDelete("{feedbackId:guid}")]
        [Authorize(Roles = "customer,admin")]
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
    }
}
