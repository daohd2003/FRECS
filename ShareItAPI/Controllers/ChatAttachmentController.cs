using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ConversationDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CloudServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/chat/attachments")]
    [ApiController]
    public class ChatAttachmentController : ControllerBase
    {
        private readonly ICloudinaryService _cloudinaryService;

        public ChatAttachmentController(ICloudinaryService cloudinaryService)
        {
            _cloudinaryService = cloudinaryService;
        }

        [HttpPost]
        [Authorize]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ApiResponse<string>("No file provided", null));

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await _cloudinaryService.UploadChatAttachmentAsync(file, userId);

            return Ok(new ApiResponse<ChatAttachmentUploadResult>("Uploaded", result));
        }
    }
}


