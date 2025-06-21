using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
