using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.ConversationRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Services.ConversationServices
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IHubContext<ChatHub> _chatHub;

        public ConversationService(
            IConversationRepository conversationRepository,
            IHubContext<ChatHub> chatHub)
        {
            _conversationRepository = conversationRepository;
            _chatHub = chatHub;
        }

        public async Task<IEnumerable<ConversationDto>> GetConversationsForUserAsync(Guid userId)
        {
            var conversations = await _conversationRepository.GetConversationsByUserIdAsync(userId);
            var unreadMap = await _conversationRepository.GetUnreadCountsAsync(userId);

            // Compute unread counts per conversation for this user
            var unreadCounts = conversations
                .ToDictionary(c => c.Id, c => 0);
            // Pull from DB in repository would be better; quick compute here for safety
            // Note: We can inject DbContext, but keep current abstraction; rely on LastMessage.IsRead for now

            // Dùng LINQ .Select để map từ Model sang DTO
            return conversations.Select(c =>
            {
                // Xác định ai là người kia trong cuộc hội thoại
                var otherUser = c.User1Id == userId ? c.User2 : c.User1;
                var lastMessageProduct = c.LastMessage?.Product;
                var lastMessageText = c.LastMessage == null
                    ? null
                    : (!string.IsNullOrEmpty(c.LastMessage.Content)
                        ? c.LastMessage.Content
                        : (!string.IsNullOrEmpty(c.LastMessage.FileName)
                            ? $"[Attachment] {c.LastMessage.FileName}"
                            : (!string.IsNullOrEmpty(c.LastMessage.AttachmentType)
                                ? $"[Attachment] {c.LastMessage.AttachmentType}"
                                : "[Attachment]")));
                // Default unread count to 0; FE will increment via SignalR for realtime and we can enhance later
                var dto = new ConversationDto
                {
                    Id = c.Id,
                    LastMessageContent = lastMessageText,
                    UpdatedAt = c.UpdatedAt,
                    IsRead = c.LastMessage?.IsRead ?? true,
                    OtherParticipant = new ParticipantDto
                    {
                        UserId = otherUser.Id,
                        FullName = otherUser.Profile?.FullName,
                        ProfilePictureUrl = otherUser.Profile?.ProfilePictureUrl,
                        Role = otherUser.Role.ToString()
                    },
                    ProductContext = lastMessageProduct == null ? null : new ProductContextDto
                    {
                        Id = lastMessageProduct.Id,
                        Name = lastMessageProduct.Name,
                        ImageUrl = lastMessageProduct.Images?.FirstOrDefault()?.ImageUrl
                    },
                    UnreadMessageCount = unreadMap.TryGetValue(c.Id, out var cnt) ? cnt : 0
                };
                return dto;
            });
        }

        public async Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(Guid conversationId, int pageNumber, int pageSize)
        {
            // Giả sử phương thức repository đã Include Product và Images
            var messages = await _conversationRepository.GetMessagesByConversationIdAsync(conversationId, pageNumber, pageSize);

            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                Content = m.Content,
                SentAt = m.SentAt,

                ProductContext = m.Product == null ? null : new ProductContextDto
                {
                    Id = m.Product.Id,
                    Name = m.Product.Name,
                    ImageUrl = m.Product.Images?.FirstOrDefault()?.ImageUrl
                },
                Attachment = string.IsNullOrEmpty(m.AttachmentUrl) ? null : new AttachmentDto
                {
                    Url = m.AttachmentUrl,
                    Type = m.AttachmentType,
                    PublicId = m.AttachmentPublicId,
                    ThumbnailUrl = m.ThumbnailUrl,
                    MimeType = m.MimeType,
                    FileName = m.FileName,
                    FileSize = m.FileSize
                }
            });
        }

        public async Task<ConversationDto> FindOrCreateConversationAsync(Guid user1Id, Guid user2Id)
        {
            // Tìm kiếm cuộc trò chuyện với đầy đủ các Include.
            var conversation = await _conversationRepository.FindAsync(user1Id, user2Id);

            // Nếu không tìm thấy, đi vào nhánh tạo mới
            if (conversation == null)
            {
                var (u1, u2) = user1Id.CompareTo(user2Id) < 0 ? (user1Id, user2Id) : (user2Id, user1Id);
                // Bước 1: Tạo một record mới trong database
                var newConversationRecord = new Conversation
                {
                    User1Id = u1,
                    User2Id = u2,
                    UpdatedAt = DateTimeHelper.GetVietnamTime()
                };

                // Bước 2: Tải lại cuộc trò chuyện vừa tạo bằng phương thức FindAsync
                conversation = await _conversationRepository.CreateAsync(newConversationRecord);

                if (conversation == null)
                {
                    throw new InvalidOperationException("Failed to retrieve the conversation immediately after creation.");
                }
            }

            var otherUser = conversation.User1Id == user1Id ? conversation.User2 : conversation.User1;
            var lastMessageProduct = conversation.LastMessage?.Product;

            return new ConversationDto
            {
                Id = conversation.Id,
                LastMessageContent = conversation.LastMessage?.Content ?? "No messages yet",
                UpdatedAt = conversation.UpdatedAt,
                IsRead = conversation.LastMessage?.IsRead ?? true,
                OtherParticipant = new ParticipantDto
                {
                    UserId = otherUser.Id,
                    FullName = otherUser.Profile?.FullName,
                    ProfilePictureUrl = otherUser.Profile?.ProfilePictureUrl
                },
                ProductContext = lastMessageProduct == null ? null : new ProductContextDto
                {
                    Id = lastMessageProduct.Id,
                    Name = lastMessageProduct.Name,
                    ImageUrl = lastMessageProduct.Images?.FirstOrDefault()?.ImageUrl
                }
            };
        }

        public async Task<ConversationDto> FindConversationAsync(Guid user1Id, Guid user2Id)
        {
            var conversation = await _conversationRepository.FindAsync(user1Id, user2Id);

            if (conversation == null)
            {
                return null; // Trả về null nếu không tìm thấy
            }

            // Map sang DTO nếu tìm thấy
            var otherUser = conversation.User1Id == user1Id ? conversation.User2 : conversation.User1;
            var lastMessageProduct = conversation.LastMessage?.Product;

            return new ConversationDto
            {
                Id = conversation.Id,
                LastMessageContent = conversation.LastMessage?.Content ?? "No messages yet",
                UpdatedAt = conversation.UpdatedAt,
                IsRead = conversation.LastMessage?.IsRead ?? true,
                OtherParticipant = new ParticipantDto
                {
                    UserId = otherUser.Id,
                    FullName = otherUser.Profile?.FullName,
                    ProfilePictureUrl = otherUser.Profile?.ProfilePictureUrl
                },
                ProductContext = lastMessageProduct == null ? null : new ProductContextDto
                {
                    Id = lastMessageProduct.Id,
                    Name = lastMessageProduct.Name,
                    ImageUrl = lastMessageProduct.Images?.FirstOrDefault()?.ImageUrl
                }
            };
        }

        public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid userId)
        {
            return await _conversationRepository.GetUnreadCountsAsync(userId);
        }

        public async Task<int> MarkMessagesAsReadAsync(Guid conversationId, Guid receiverId)
        {
            return await _conversationRepository.MarkMessagesAsReadAsync(conversationId, receiverId);
        }

        public async Task<MessageDto?> SendViolationMessageToProviderAsync(
            Guid? staffId,
            Guid providerId,
            Guid productId,
            string productName,
            string reason,
            string violatedTerms)
        {
            // Nếu không có staffId, return null
            if (!staffId.HasValue)
            {
                Console.WriteLine("[CHAT] No staff logged in - cannot send chat notification");
                return null;
            }

            try
            {
                // 1. Tạo/Tìm conversation
                var conversation = await FindOrCreateConversationAsync(staffId.Value, providerId);

                // 2. Build message content (max 1000 chars)
                var messageContent = $@"⚠️ Content Violation Detected

Product: {productName}
Reason: {reason}
Violated: {violatedTerms}
Time: {DateTimeHelper.GetVietnamTime():dd/MM/yyyy HH:mm}

Please update your product to comply with our guidelines.
Reply here or contact support if you need help.

🤖 Automated message from ShareIt Content Moderation";

                if (messageContent.Length > 1000)
                {
                    messageContent = messageContent.Substring(0, 997) + "...";
                }

                // 3. Tạo message
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversation.Id,
                    SenderId = staffId.Value,
                    ReceiverId = providerId,
                    ProductId = productId,
                    Content = messageContent,
                    SentAt = DateTimeHelper.GetVietnamTime(),
                    IsRead = false
                };

                // 4. Lưu message
                await _conversationRepository.CreateMessageAsync(message);

                // 5. Update conversation
                await _conversationRepository.UpdateConversationLastMessageAsync(conversation.Id, message.Id);

                // 6. Load message với Product
                var messageWithProduct = await _conversationRepository.GetMessageByIdAsync(message.Id);

                // 7. Map sang DTO
                var messageDto = new MessageDto
                {
                    Id = messageWithProduct.Id,
                    ConversationId = messageWithProduct.ConversationId,
                    SenderId = messageWithProduct.SenderId,
                    Content = messageWithProduct.Content,
                    SentAt = messageWithProduct.SentAt,
                    ProductContext = messageWithProduct.Product == null ? null : new ProductContextDto
                    {
                        Id = messageWithProduct.Product.Id,
                        Name = messageWithProduct.Product.Name,
                        ImageUrl = messageWithProduct.Product.Images?.FirstOrDefault()?.ImageUrl
                    }
                };

                // 8. Gửi real-time qua SignalR
                await _chatHub.Clients.User(providerId.ToString()).SendAsync("ReceiveMessage", messageDto);

                Console.WriteLine($"[CHAT] Violation message sent from Staff {staffId} to Provider {providerId}");
                return messageDto;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send violation chat: {ex.Message}");
                return null;
            }
        }
    }
}
