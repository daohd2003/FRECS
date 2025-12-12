using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.FeedbackRepositories
{
    public interface IFeedbackRepository : IRepository<Feedback>
    {
        Task<IEnumerable<Feedback>> GetFeedbacksByTargetAsync(FeedbackTargetType targetType, Guid targetId);
        Task<IEnumerable<Feedback>> GetFeedbacksByCustomerIdAsync(Guid customerId);
        Task<IEnumerable<Feedback>> GetFeedbacksByProviderIdAsync(Guid providerId);
        Task<bool> HasUserFeedbackedOrderItemAsync(Guid customerId, Guid orderItemId);
        Task<bool> HasUserFeedbackedOrderAsync(Guid customerId, Guid orderId);
        Task<PaginatedResponse<Feedback>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize);
        Task<PaginatedResponse<Feedback>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize, bool includeBlocked);
        Task<IEnumerable<Feedback>> GetFeedbacksByProductAndCustomerAsync(Guid productId, Guid customerId);
        
        // Feedback Management
        Task<PaginatedResponse<Feedback>> GetAllFeedbacksWithFilterAsync(FeedbackFilterDto filter);
        Task<Feedback?> GetFeedbackDetailAsync(Guid feedbackId);
        Task<bool> BlockFeedbackAsync(Guid feedbackId, Guid blockedById);
        Task<bool> UnblockFeedbackAsync(Guid feedbackId);
        Task<FeedbackStatisticsDto> GetFeedbackStatisticsAsync();
        Task<IEnumerable<Feedback>> GetFlaggedFeedbacksAsync();
    }
}
