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
    }
}

