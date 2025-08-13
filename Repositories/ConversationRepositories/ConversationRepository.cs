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

                // Sửa lại chuỗi Include ở đây
                .Include(c => c.LastMessage)
                    .ThenInclude(lm => lm.Product)
                        .ThenInclude(p => p.Images)

                .Include(c => c.User1).ThenInclude(u => u.Profile)
                .Include(c => c.User2).ThenInclude(u => u.Profile)
                .AsNoTracking()
                .AsSplitQuery()
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> GetMessagesByConversationIdAsync(Guid conversationId, int pageNumber, int pageSize)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)

                // Tải các dữ liệu liên quan
                .Include(m => m.Product)
                    .ThenInclude(p => p.Images)

                .OrderByDescending(m => m.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();
        }

        public async Task<Conversation> FindAsync(Guid user1Id, Guid user2Id)
        {
            var (u1, u2) = user1Id.CompareTo(user2Id) < 0 ? (user1Id, user2Id) : (user2Id, user1Id);

            return await _context.Conversations
                .Include(c => c.User1).ThenInclude(u => u.Profile)
                .Include(c => c.User2).ThenInclude(u => u.Profile)
                .Include(c => c.LastMessage)
                    .ThenInclude(lm => lm.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(c => c.User1Id == u1 && c.User2Id == u2);
        }

        public async Task<Conversation> CreateAsync(Conversation conversation)
        {
            await _context.Conversations.AddAsync(conversation);
            await _context.SaveChangesAsync();


            await _context.Entry(conversation).Reference(c => c.User1).Query().Include(u => u.Profile).LoadAsync();
            await _context.Entry(conversation).Reference(c => c.User2).Query().Include(u => u.Profile).LoadAsync();
            // LastMessage sẽ là null, nhưng gọi LoadAsync vẫn an toàn
            await _context.Entry(conversation).Reference(c => c.LastMessage).LoadAsync();

            return conversation;
            /*return await FindAsync(conversation.User1Id, conversation.User2Id);*/
        }
    }
}
