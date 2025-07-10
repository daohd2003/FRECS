using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProfileDtos;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CloudServices;
using Services.ProfileServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/profile")]
    [Authorize(Roles = "admin,customer,provider")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ICloudinaryService _cloudinaryService;

        public ProfileController(IProfileService profileService, ICloudinaryService cloudinaryService)
        {
            _profileService = profileService;
            _cloudinaryService = cloudinaryService;
        }

        // GET: api/profile/{userId}
        [HttpGet("{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProfile(Guid userId)
        {
            var profile = await _profileService.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound(new ApiResponse<string>("Profile not found", null));

            return Ok(new ApiResponse<Profile>("Profile retrieved successfully", profile));
        }

        // PUT: api/profile/{userId}
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateProfile(Guid userId, [FromBody] ProfileUpdateDto profileDto)
        {
            var existingProfile = await _profileService.GetByUserIdAsync(userId);
            if (existingProfile == null)
            {
                return NotFound(new ApiResponse<string>("Profile not found to update.", null));
            }

            existingProfile.FullName = profileDto.FullName;
            existingProfile.Phone = profileDto.Phone;
            existingProfile.Address = profileDto.Address;

            await _profileService.UpdateAsync(existingProfile);

            return Ok(new ApiResponse<string>("Profile updated successfully", null));
        }

        [HttpPost("upload-image")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> UploadAvatar(IFormFile file, string projectName = "ShareIt", string folderType = "profile_pics")
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                string imageUrl = await _cloudinaryService.UploadImage(file, userId, projectName, folderType);

                return Ok(new ApiResponse<string>("Image uploaded successfully", imageUrl));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Image upload failed: {ex.Message}", null));
            }
        }

        [HttpGet("header-info")]
        public async Task<IActionResult> GetHeaderInfo()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized();
            }

            var profile = await _profileService.GetByUserIdAsync(userId);
            if (profile == null)
            {
                return NotFound("Profile not found.");
            }

            var headerInfo = new UserHeaderInfoDto
            {
                FullName = profile.FullName,
                ProfilePictureUrl = profile.ProfilePictureUrl
            };

            return Ok(headerInfo);
        }
    }
}