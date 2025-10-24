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
            // Load conversations + compute unread count per conversation for this user
            var conversations = await _context.Conversations
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

            var conversationIds = conversations.Select(c => c.Id).ToList();
            var unreadCounts = await _context.Messages
                .Where(m => conversationIds.Contains(m.ConversationId) && m.ReceiverId == userId && !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToListAsync();

            var map = unreadCounts.ToDictionary(x => x.ConversationId, x => x.Count);
            foreach (var c in conversations)
            {
                // stash temporarily using LastMessageId as carrier is wrong; instead we will attach via service mapping
                // nothing to do here; service will read counts using local dictionary
            }

            return conversations;
        }

        public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid userId)
        {
            var unreadCounts = await _context.Messages
                .Where(m => m.ReceiverId == userId && !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToListAsync();
            return unreadCounts.ToDictionary(x => x.ConversationId, x => x.Count);
        }

        public async Task<int> MarkMessagesAsReadAsync(Guid conversationId, Guid receiverId)
        {
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.ReceiverId == receiverId && !m.IsRead)
                .ToListAsync();

            if (messages.Count == 0) return 0;

            foreach (var m in messages)
            {
                m.IsRead = true;
            }
            await _context.SaveChangesAsync();
            return messages.Count;
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

        // New methods for violation message functionality
        public async Task<Message> CreateMessageAsync(Message message)
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task UpdateConversationLastMessageAsync(Guid conversationId, Guid messageId)
        {
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation != null)
            {
                conversation.LastMessageId = messageId;
                conversation.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Message?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.Messages
                .Include(m => m.Product)
                    .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }
    }
}
