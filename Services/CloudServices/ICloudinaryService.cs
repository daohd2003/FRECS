using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Http;

namespace Services.CloudServices
{
    public interface ICloudinaryService
    {
        // Profile upload - saves to Profile table
        Task<string> UploadImage(IFormFile file, Guid userId, string projectName, string folderType);
        
        // Generic upload - returns URL only, no DB save
        Task<ImageUploadResult> UploadSingleImageAsync(IFormFile file, Guid userId, string projectName, string folderType);
        
        // Category upload - dedicated for categories, validates and uploads with specific settings
        Task<ImageUploadResult> UploadCategoryImageAsync(IFormFile file, Guid userId);
        
        // Multiple images upload
        Task<List<ImageUploadResult>> UploadMultipleImagesAsync(IFormFileCollection files, Guid userId, string projectName, string folderType);
        
        // Delete image
        Task<bool> DeleteImageAsync(string publicId);

        // For chat attachments (image/video/file)
        Task<ChatAttachmentUploadResult> UploadChatAttachmentAsync(IFormFile file, Guid userId);
    }
}
