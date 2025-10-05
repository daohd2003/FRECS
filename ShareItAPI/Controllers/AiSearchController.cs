using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.AI;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiSearchController : ControllerBase
    {
        private readonly IAiSearchService _aiSearchService;

        public AiSearchController(IAiSearchService aiSearchService)
        {
            _aiSearchService = aiSearchService;
        }

        [HttpGet("ask")]
        public async Task<IActionResult> Ask([FromQuery] string question, [FromQuery] Guid? userId = null)
        {
            if (string.IsNullOrEmpty(question))
                return BadRequest("Question is required.");

            // If userId not provided in query, try to get from authenticated user
            if (!userId.HasValue && User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var parsedUserId))
                {
                    userId = parsedUserId;
                }
            }

            var answer = await _aiSearchService.AskAboutFRECSAsync(question, userId);
            var responseDto = new AiSearchResponseDto
            {
                Answer = answer
            };
            return Ok(responseDto);
        }
    }
}
