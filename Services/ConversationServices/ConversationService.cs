using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.Models;
using Repositories.ConversationRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ConversationServices
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepository;

        public ConversationService(IConversationRepository conversationRepository)
        {
            _conversationRepository = conversationRepository;
        }

        public async Task<IEnumerable<ConversationDto>> GetConversationsForUserAsync(Guid userId)
        {
            var conversations = await _conversationRepository.GetConversationsByUserIdAsync(userId);

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
                return new ConversationDto
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
                    }
                };
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
                    UpdatedAt = DateTime.UtcNow
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
    }
}
