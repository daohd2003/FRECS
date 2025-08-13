using BusinessObject.DTOs.ConversationDtos;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Http;

namespace Services.CloudServices
{
    public interface ICloudinaryService
    {
        Task<string> UploadImage(IFormFile file, Guid userId, string projectName, string folderType);
        Task<ImageUploadResult> UploadSingleImageAsync(IFormFile file, Guid userId, string projectName, string folderType);
        Task<List<ImageUploadResult>> UploadMultipleImagesAsync(IFormFileCollection files, Guid userId, string projectName, string folderType);
        Task<bool> DeleteImageAsync(string publicId);

        // For chat attachments (image/video/file)
        Task<ChatAttachmentUploadResult> UploadChatAttachmentAsync(IFormFile file, Guid userId);
    }
}
