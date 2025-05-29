using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.CloudServices;
using Services.ProfileServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
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
        public async Task<IActionResult> GetProfile(Guid userId)
        {
            var profile = await _profileService.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            return Ok(profile);
        }

        // PUT: api/profile/{userId}
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateProfile(Guid userId, [FromBody] Profile updatedProfile)
        {
            if (userId != updatedProfile.UserId)
                return BadRequest("User ID mismatch");

            await _profileService.UpdateAsync(updatedProfile);

            return Ok();
        }

        [HttpPost("upload-image")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<ActionResult<string>> UploadAvatar(IFormFile file, string projectName = "ShareIt", string folderType = "profile_pics")
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                string ImageUrl = await _cloudinaryService.UploadImage(file, userId, projectName, folderType);

                return Ok(ImageUrl);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
