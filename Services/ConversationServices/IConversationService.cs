using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ConversationServices
{
    public interface IConversationService
    {
        Task<IEnumerable<ConversationDto>> GetConversationsForUserAsync(Guid userId);
        Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(Guid conversationId, int pageNumber, int pageSize);
        Task<ConversationDto> FindOrCreateConversationAsync(Guid user1Id, Guid user2Id);
        Task<ConversationDto> FindConversationAsync(Guid user1Id, Guid user2Id);
        Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid userId);
        Task<int> MarkMessagesAsReadAsync(Guid conversationId, Guid receiverId);

        // New method for sending violation messages
        Task<MessageDto?> SendViolationMessageToProviderAsync(
            Guid? staffId,
            Guid providerId,
            Guid productId,
            string productName,
            string reason,
            string violatedTerms
        );
    }
}
