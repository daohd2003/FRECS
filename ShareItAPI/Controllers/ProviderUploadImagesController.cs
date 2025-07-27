using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CloudServices;
using Services.ProfileServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProviderUploadImagesController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ICloudinaryService _cloudinaryService;
        public ProviderUploadImagesController(IProfileService profileService, ICloudinaryService cloudinaryService)
        {
            _profileService = profileService;
            _cloudinaryService = cloudinaryService;
        }
        [HttpPost("upload-images")]
        [Authorize]
        public async Task<IActionResult> UploadProductImages([FromForm] IFormFileCollection images)
        {
            if (images == null || images.Count == 0)
            {
                return BadRequest(new ApiResponse<string>("No images provided.", null));
            }
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                // Dùng phương thức upload nhiều ảnh cho cả 2 trường hợp
                var results = await _cloudinaryService.UploadMultipleImagesAsync(images, userId, "ShareIt", "product_pics");
                return Ok(new ApiResponse<List<ImageUploadResult>>("Images uploaded successfully.", results));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpDelete("delete-image")]
        [Authorize]
        public async Task<IActionResult> DeleteImage([FromQuery] string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
            {
                return BadRequest("PublicId is required.");
            }

            try
            {
                var success = await _cloudinaryService.DeleteImageAsync(publicId);
                if (success)
                {
                    return Ok(new { message = "Image deleted successfully." });
                }
                return StatusCode(500, new { message = "Failed to delete image from Cloudinary." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
