using BusinessObject.DTOs.ProductDto;

namespace Services.ContentModeration
{
    public interface IContentModerationService
    {
        Task<ContentModerationResultDTO> CheckProductContentAsync(string name, string? description);
    }
}

