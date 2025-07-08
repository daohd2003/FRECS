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
        private readonly ShareItDbContext _context;

        public ChatHub(ShareItDbContext context)
        {
            _context = context;
        }
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                lock (UserConnections)
                {
                    UserConnections[userId] = Context.ConnectionId;
                }
            }

            await base.OnConnectedAsync();
        }

        public async Task SendMessageAsync(string conversationId, string receiverId, string content, string? productId)
        {
            var senderIdString = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderIdString) || string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(content))
            {
                return; // Dữ liệu không hợp lệ
            }
            Guid? productGuid = !string.IsNullOrEmpty(productId) ? Guid.Parse(productId) : null;
            var senderId = Guid.Parse(senderIdString);
            var conversationGuid = Guid.Parse(conversationId);
            var receiverGuid = Guid.Parse(receiverId);

            // 1. Tạo tin nhắn mới và gán trực tiếp vào đúng ConversationId
            var message = new Message
            {
                ConversationId = conversationGuid,
                SenderId = senderId,
                ReceiverId = receiverGuid,
                Content = content,
                ProductId = productGuid,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            Product productWithImages = null;
            if (productGuid.HasValue)
            {
                productWithImages = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productGuid.Value);
            }

            message.Product = productWithImages;

            _context.Messages.Add(message);
            await _context.Entry(message).Reference(m => m.Product).LoadAsync();
            await _context.SaveChangesAsync();

            // 2. Cập nhật Conversation tương ứng
            var conversation = await _context.Conversations.FindAsync(conversationGuid);
            if (conversation != null)
            {
                conversation.LastMessageId = message.Id;
                conversation.UpdatedAt = message.SentAt;
                await _context.SaveChangesAsync();
            }

            // 3. Tạo DTO để gửi đi
            var images = message.Product?.Images;
            var displayImage = images?.FirstOrDefault(i => i.IsPrimary) ?? images?.FirstOrDefault();
            var messageDto = new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead,
                ProductContext = message.Product == null ? null : new ProductContextDto
                {
                    Id = message.Product.Id,
                    Name = message.Product.Name,
                    ImageUrl = displayImage?.ImageUrl
                }
            };

            // 4. Gửi DTO đến người nhận và người gửi (Caller)
            if (UserConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", messageDto);
            }
            await Clients.Caller.SendAsync("ReceiveMessage", messageDto);
        }
    }
}
