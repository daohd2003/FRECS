using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.AI;

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
        public async Task<IActionResult> Ask([FromQuery] string question)
        {
            if (string.IsNullOrEmpty(question))
                return BadRequest("Question is required.");

            var answer = await _aiSearchService.AskAboutShareITAsync(question);
            var responseDto = new AiSearchResponseDto
            {
                Answer = answer
            };
            return Ok(responseDto);
        }
    }
}
