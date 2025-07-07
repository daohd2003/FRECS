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
            return conversations.Select(c => {
                // Xác định ai là người kia trong cuộc hội thoại
                var otherUser = c.User1Id == userId ? c.User2 : c.User1;

                return new ConversationDto
                {
                    Id = c.Id,
                    LastMessageContent = c.LastMessage?.Content,
                    UpdatedAt = c.UpdatedAt,
                    IsRead = c.LastMessage?.IsRead ?? true,
                    OtherParticipant = new ParticipantDto
                    {
                        UserId = otherUser.Id,
                        FullName = otherUser.Profile?.FullName,
                        ProfilePictureUrl = otherUser.Profile?.ProfilePictureUrl
                    }
                };
            });
        }

        public async Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(Guid conversationId, int pageNumber, int pageSize)
        {
            var messages = await _conversationRepository.GetMessagesByConversationIdAsync(conversationId, pageNumber, pageSize);

            // Logic map sang DTO giữ nguyên
            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                Content = m.Content,
                SentAt = m.SentAt
            });
        }

        public async Task<ConversationDto> FindOrCreateConversationAsync(Guid user1Id, Guid user2Id, Guid? productId)
        {
            var conversation = await _conversationRepository.FindAsync(user1Id, user2Id, productId);

            if (conversation == null)
            {
                conversation = await _conversationRepository.CreateAsync(new Conversation
                {
                    User1Id = user1Id,
                    User2Id = user2Id,
                    ProductId = productId,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Map to DTO before returning
            var otherUser = conversation.User1Id == user1Id ? conversation.User2 : conversation.User1;
            return new ConversationDto
            {
                Id = conversation.Id,
                LastMessageContent = conversation.LastMessage?.Content,
                UpdatedAt = conversation.UpdatedAt,
                IsRead = conversation.LastMessage?.IsRead ?? true,
                OtherParticipant = new ParticipantDto
                {
                    UserId = otherUser.Id,
                    FullName = otherUser.Profile?.FullName,
                    ProfilePictureUrl = otherUser.Profile?.ProfilePictureUrl
                }
            };
        }
    }
}
