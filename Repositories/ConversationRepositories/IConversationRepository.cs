using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ConversationRepositories
{
    public interface IConversationRepository
    {
        Task<IEnumerable<Conversation>> GetConversationsByUserIdAsync(Guid userId);
        Task<IEnumerable<Message>> GetMessagesByConversationIdAsync(Guid conversationId, int pageNumber, int pageSize);
        Task<Conversation> FindAsync(Guid user1Id, Guid user2Id);
        Task<Conversation> CreateAsync(Conversation conversation);
    }
}
