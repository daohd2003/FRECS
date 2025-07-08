using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using Services.ConversationServices;
using BusinessObject.DTOs.ConversationDtos;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        // Inject the service, not the context
        private readonly IConversationService _conversationService;

        public ConversationsController(IConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        // GET: api/conversations
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            var userId = Guid.Parse(userIdString);

            var conversationDtos = await _conversationService.GetConversationsForUserAsync(userId);

            return Ok(conversationDtos);
        }

        // GET: api/conversations/{id}/messages
        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetConversationMessages(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var messages = await _conversationService.GetMessagesForConversationAsync(id, pageNumber, pageSize);
            return Ok(messages);
        }

        [HttpPost("find-or-create")]
        public async Task<IActionResult> FindOrCreateConversation([FromBody] FindConversationRequestDto request)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            var currentUserId = Guid.Parse(userIdString);

            if (currentUserId == request.RecipientId)
            {
                return BadRequest("Cannot create a conversation with yourself.");
            }

            var conversationDto = await _conversationService.FindOrCreateConversationAsync(currentUserId, request.RecipientId);

            return Ok(conversationDto);
        }

        [HttpGet("find-by-users")]
        public async Task<IActionResult> FindConversationByUsers([FromQuery] Guid user1Id, [FromQuery] Guid user2Id)
        {
            var conversationDto = await _conversationService.FindConversationAsync(user1Id, user2Id);

            if (conversationDto == null)
            {
                return NotFound("No conversation found between the two users.");
            }

            return Ok(conversationDto);
        }
    }
}
