using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Services.TryOnImageServices
{
    public class AICloudinaryService : IAICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<AICloudinaryService> _logger;

        public AICloudinaryService(
            [FromKeyedServices("AICloudinary")] Cloudinary cloudinary,
            ILogger<AICloudinaryService> logger)
        {
            _cloudinary = cloudinary;
            _logger = logger;
        }

        public async Task<bool> DeleteImageAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId) || publicId == "unknown")
            {
                _logger.LogWarning("Invalid publicId for deletion: {PublicId}", publicId);
                return false;
            }

            try
            {
                var deletionParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deletionParams);

                var success = result.Result?.ToLower() == "ok" || result.Result?.ToLower() == "not found";
                
                if (success)
                {
                    _logger.LogInformation("Deleted AI Try-On image from Cloudinary: {PublicId}", publicId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete AI Try-On image: {PublicId}, Result: {Result}", publicId, result.Result);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AI Try-On image from Cloudinary: {PublicId}", publicId);
                return false;
            }
        }
    }
}
