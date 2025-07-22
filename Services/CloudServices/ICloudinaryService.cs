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
    }
}
