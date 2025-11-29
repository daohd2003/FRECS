using BusinessObject.DTOs.ProductDto;

namespace Services.ContentModeration
{
    public interface IContentModerationService
    {
        /// <summary>
        /// Check single content string (for Feedback, comments, etc.)
        /// </summary>
        Task<ContentModerationResultDTO> CheckContentAsync(string content);
        
        /// <summary>
        /// Check product content (name + description)
        /// </summary>
        Task<ContentModerationResultDTO> CheckProductContentAsync(string name, string? description);
        
        /// <summary>
        /// Check feedback content (comment + provider response)
        /// Can check either comment only, response only, or both
        /// </summary>
        Task<ContentModerationResultDTO> CheckFeedbackContentAsync(string? comment, string? providerResponse = null);
    }
}

