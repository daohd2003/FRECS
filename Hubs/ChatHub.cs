using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.Models;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly Dictionary<string, string> UserConnections = new Dictionary<string, string>();
        private readonly ShareItDbContext _context; // Your DbContext

        public ChatHub(ShareItDbContext context)
        {
            _context = context;
        }

        // OnConnectedAsync and OnDisconnectedAsync remain the same...

        public async Task SendMessageAsync(string conversationId, string receiverId, string content)
        {
            var senderIdString = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderIdString) || string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(content))
            {
                return; // Invalid request
            }

            var senderId = Guid.Parse(senderIdString);
            var conversationGuid = Guid.Parse(conversationId);
            var receiverGuid = Guid.Parse(receiverId);

            // 1. Find or create the conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    (c.User1Id == senderId && c.User2Id == receiverGuid) ||
                    (c.User1Id == receiverGuid && c.User2Id == senderId));

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    User1Id = senderId,
                    User2Id = receiverGuid,
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync(); // Save to get the new Conversation ID
            }

            // 2. Create the message and link it to the conversation
            var message = new Message
            {
                ConversationId = conversationGuid,
                SenderId = senderId,
                ReceiverId = receiverGuid,
                Content = content,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync(); // Save to get the new Message ID

            // 3. Update the conversation with the last message details
            conversation.LastMessageId = message.Id;
            conversation.UpdatedAt = message.SentAt;
            await _context.SaveChangesAsync();

            // 4. Tạo một DTO từ đối tượng Message vừa lưu
            var messageDto = new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead
            };

            // 5. Gửi DTO đi thay vì đối tượng EF gốc
            if (UserConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", messageDto);
            }

            await Clients.Caller.SendAsync("ReceiveMessage", messageDto);
        }
    }
}
