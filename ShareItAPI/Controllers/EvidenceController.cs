using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CloudServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EvidenceController : ControllerBase
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<EvidenceController> _logger;

        public EvidenceController(ICloudinaryService cloudinaryService, ILogger<EvidenceController> logger)
        {
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadEvidenceImages([FromForm] List<IFormFile> files)
        {
            try
            {
                if (files == null || !files.Any())
                {
                    return BadRequest(new { message = "No files provided" });
                }

                if (files.Count > 5)
                {
                    return BadRequest(new { message = "Maximum 5 images allowed" });
                }

                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var uploadResults = new List<string>();

                foreach (var file in files)
                {
                    // Validate file
                    if (file.Length == 0) continue;

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var extension = Path.GetExtension(file.FileName)?.ToLower() ?? "";
                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { message = $"Invalid file type: {file.FileName}. Only JPG, JPEG, PNG, GIF, WEBP allowed." });
                    }

                    if (file.Length > 5 * 1024 * 1024) // 5MB
                    {
                        return BadRequest(new { message = $"File {file.FileName} exceeds 5MB limit" });
                    }

                    // Upload to Cloudinary
                    var result = await _cloudinaryService.UploadSingleImageAsync(file, userId, "ShareIt", "evidence");
                    uploadResults.Add(result.ImageUrl);
                }

                return Ok(new { 
                    success = true, 
                    message = "Images uploaded successfully", 
                    urls = uploadResults 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading evidence images for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return StatusCode(500, new { message = "Failed to upload images", error = ex.Message });
            }
        }
    }
}
