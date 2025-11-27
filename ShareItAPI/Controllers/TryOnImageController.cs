using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.TryOnDtos;
using Common.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.TryOnImageServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/try-on-images")]
    [ApiController]
    [Authorize]
    public class TryOnImageController : ControllerBase
    {
        private readonly ITryOnImageService _tryOnImageService;
        private readonly ILogger<TryOnImageController> _logger;

        public TryOnImageController(
            ITryOnImageService tryOnImageService,
            ILogger<TryOnImageController> logger)
        {
            _tryOnImageService = tryOnImageService;
            _logger = logger;
        }

        /// <summary>
        /// Lưu ảnh Try-On mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveTryOnImage([FromBody] SaveTryOnImageRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ApiResponse<string>("User not authenticated", null));

                if (string.IsNullOrEmpty(request.ImageUrl) || string.IsNullOrEmpty(request.CloudinaryPublicId))
                    return BadRequest(new ApiResponse<string>("ImageUrl and CloudinaryPublicId are required", null));

                var result = await _tryOnImageService.SaveTryOnImageAsync(userId.Value, request);
                
                return Ok(new ApiResponse<TryOnImageDto>("Try-On image saved successfully", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Try-On image");
                return StatusCode(500, new ApiResponse<string>("An error occurred while saving the image", null));
            }
        }

        /// <summary>
        /// Lấy danh sách ảnh Try-On của customer hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyTryOnImages([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ApiResponse<string>("User not authenticated", null));

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 50) pageSize = 20;

                var result = await _tryOnImageService.GetCustomerTryOnImagesAsync(userId.Value, pageNumber, pageSize);
                
                return Ok(new ApiResponse<TryOnImageListResponse>("Try-On images retrieved successfully", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Try-On images");
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving images", null));
            }
        }

        /// <summary>
        /// Lấy chi tiết một ảnh Try-On
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTryOnImage(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ApiResponse<string>("User not authenticated", null));

                var result = await _tryOnImageService.GetTryOnImageByIdAsync(id, userId.Value);
                
                if (result == null)
                    return NotFound(new ApiResponse<string>("Try-On image not found", null));

                return Ok(new ApiResponse<TryOnImageDto>("Try-On image retrieved successfully", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Try-On image {ImageId}", id);
                return StatusCode(500, new ApiResponse<string>("An error occurred while retrieving the image", null));
            }
        }

        /// <summary>
        /// Xóa ảnh Try-On
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTryOnImage(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ApiResponse<string>("User not authenticated", null));

                var result = await _tryOnImageService.DeleteTryOnImageAsync(id, userId.Value);
                
                if (!result)
                    return NotFound(new ApiResponse<string>("Try-On image not found or already deleted", null));

                return Ok(new ApiResponse<string>("Try-On image deleted successfully", null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Try-On image {ImageId}", id);
                return StatusCode(500, new ApiResponse<string>("An error occurred while deleting the image", null));
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
