using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using Services.ConversationServices;
using Services.ProductServices;
using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.DTOs.PagingDto;

namespace ShareItAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IProductService _productService;

        public ConversationsController(
            IConversationService conversationService,
            IProductService productService)
        {
            _conversationService = conversationService;
            _productService = productService;
        }

        /// <summary>
        /// Feature: Chat with provider / Chat with staff
        /// Get all conversations for the current user (for messaging with providers or staff).
        /// </summary>
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

        [HttpPost("{id}/mark-read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            var currentUserId = Guid.Parse(userIdString);

            var updated = await _conversationService.MarkMessagesAsReadAsync(id, currentUserId);
            return Ok(new { updated });
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

        /// <summary>
        /// Get products for chat product picker based on user role and recipient
        /// - Customer: Gets available products (what they see in browse page)
        /// - Provider chatting with Customer/Provider: Only their own products
        /// - Provider chatting with Staff/Admin: Can choose "my" (own products) or "all" (browse products)
        /// - Admin/Staff chatting with Provider: Gets products owned by that provider
        /// - Admin/Staff chatting with Customer: Gets all available products (what customer can see)
        /// </summary>
        [HttpGet("products-for-chat")]
        public IActionResult GetProductsForChat(
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? recipientId,
            [FromQuery] string? recipientRole,
            [FromQuery] string? source, // "my" for own products, "all" for browse products (Provider only)
            [FromQuery] Guid? providerId, // Legacy parameter, kept for backward compatibility
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            var userId = Guid.Parse(userIdString);

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value?.ToLower() ?? "customer";

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 20) pageSize = 20; // Limit max page size for performance

            IQueryable<BusinessObject.DTOs.ProductDto.ProductDTO> query;

            // Get products based on user role and who they're chatting with
            if (userRole == "provider")
            {
                var sourceLower = source?.ToLower() ?? "my";
                
                // Provider can always choose between own products or all browse products
                if (sourceLower == "all")
                {
                    // Show all available products (like browse page)
                    query = _productService.GetAll();
                }
                else
                {
                    // Default (source=my): Only their own products (all statuses except deleted)
                    query = _productService.GetAllNoFilter()
                        .Where(p => p.ProviderId == userId && p.AvailabilityStatus.ToLower() != "deleted");
                }
            }
            else if (userRole == "admin" || userRole == "staff")
            {
                // Admin/Staff: Products depend on who they're chatting with
                var recipientRoleLower = recipientRole?.ToLower() ?? "";
                
                if (recipientRoleLower == "provider" && recipientId.HasValue)
                {
                    // Chatting with Provider: Show products owned by that provider
                    query = _productService.GetAllNoFilter()
                        .Where(p => p.ProviderId == recipientId.Value && p.AvailabilityStatus.ToLower() != "deleted");
                }
                else if (recipientRoleLower == "customer")
                {
                    // Chatting with Customer: Show all available products (what customer can see in browse)
                    query = _productService.GetAll();
                }
                else
                {
                    // Default: All available products, optionally filter by providerId (legacy)
                    query = _productService.GetAllNoFilter()
                        .Where(p => p.AvailabilityStatus.ToLower() != "deleted");
                    
                    if (providerId.HasValue)
                    {
                        query = query.Where(p => p.ProviderId == providerId.Value);
                    }
                }
            }
            else
            {
                // Customer: Only available products from the specific provider they're chatting with
                query = _productService.GetAll();
                
                // Use recipientId (new) or providerId (legacy) for filtering
                var providerIdToFilter = recipientId ?? providerId;
                if (providerIdToFilter.HasValue)
                {
                    query = query.Where(p => p.ProviderId == providerIdToFilter.Value);
                }
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(lowerSearchTerm) ||
                    (p.Category != null && p.Category.ToLower().Contains(lowerSearchTerm))
                );
            }

            var totalCount = query.Count();

            // Map to lightweight DTO for chat picker
            var items = query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ChatProductPickerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    ImageUrl = p.PrimaryImagesUrl,
                    PricePerDay = p.PricePerDay,
                    PurchasePrice = p.PurchasePrice > 0 ? p.PurchasePrice : null,
                    ProviderName = p.ProviderName,
                    Category = p.Category
                })
                .ToList();

            return Ok(new PagedResult<ChatProductPickerDto>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            });
        }
    }
}
