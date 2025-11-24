using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;

namespace Services.FeedbackServices
{
    public interface IFeedbackService
    {
        Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto dto, Guid customerId);
        Task<FeedbackResponseDto> GetFeedbackByIdAsync(Guid feedbackId);
        Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByTargetAsync(FeedbackTargetType targetType, Guid targetId);
        Task<IEnumerable<FeedbackResponseDto>> GetCustomerFeedbacksAsync(Guid customerId);
        Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByProviderIdAsync(Guid providerId, Guid currentUserId, bool isAdmin);
        Task UpdateFeedbackAsync(Guid feedbackId, FeedbackRequestDto dto, Guid currentUserId);
        Task DeleteFeedbackAsync(Guid feedbackId, Guid currentUserId);

        Task SubmitProviderResponseAsync(Guid feedbackId, SubmitProviderResponseDto responseDto, Guid providerOrAdminId);

        Task RecalculateProductRatingAsync(Guid productId);
        Task<ApiResponse<PaginatedResponse<FeedbackResponseDto>>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize);
        Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByProductAndCustomerAsync(Guid productId, Guid customerId);
    }
}
