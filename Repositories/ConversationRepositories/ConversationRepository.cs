using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ConversationRepositories
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly ShareItDbContext _context;

        public ConversationRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Conversation>> GetConversationsByUserIdAsync(Guid userId)
        {
            return await _context.Conversations
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .Include(c => c.LastMessage)
                .Include(c => c.User1).ThenInclude(u => u.Profile)
                .Include(c => c.User2).ThenInclude(u => u.Profile)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> GetMessagesByConversationIdAsync(Guid conversationId, int pageNumber, int pageSize)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<Conversation> FindAsync(Guid user1Id, Guid user2Id, Guid? productId)
        {
            return await _context.Conversations
                .Include(c => c.User1).ThenInclude(u => u.Profile)
                .Include(c => c.User2).ThenInclude(u => u.Profile)
                .Include(c => c.LastMessage)
                .FirstOrDefaultAsync(c =>
                    // Điều kiện tìm kiếm bây giờ bao gồm cả ProductId
                    (c.User1Id == user1Id && c.User2Id == user2Id && c.ProductId == productId) ||
                    (c.User1Id == user2Id && c.User2Id == user1Id && c.ProductId == productId));
        }

        public async Task<Conversation> CreateAsync(Conversation conversation)
        {
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            // Sau khi lưu, ta cần load lại thông tin User để trả về đầy đủ
            return await FindAsync(conversation.User1Id, conversation.User2Id, conversation.ProductId);
        }
    }
}
