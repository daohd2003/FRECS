using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Services.ContentModeration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.ProductRepositories;
using Services.ConversationServices;
using Services.CloudServices;


namespace Services.ProductServices
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly IContentModerationService _contentModerationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationService _conversationService;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductService(
            IProductRepository productRepository,
            IMapper mapper, 
            IContentModerationService contentModerationService,
            IServiceProvider serviceProvider,
            IConversationService conversationService,
            ICloudinaryService cloudinaryService)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _contentModerationService = contentModerationService;
            _serviceProvider = serviceProvider;
            _conversationService = conversationService;
            _cloudinaryService = cloudinaryService;
        }

        public IQueryable<ProductDTO> GetAll()
        {
            return _productRepository.GetAllWithIncludes()
                .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider);
        }

        public async Task<ProductDTO?> GetByIdAsync(Guid id)
        {
            var product = await _productRepository.GetProductWithImagesByIdAsync(id);
            return product == null ? null : _mapper.Map<ProductDTO>(product);
        }



        public async Task<ProductDTO> AddAsync(ProductRequestDTO dto)
            {
                // Bước 1: Tạo product với images thông qua repository
                var newProduct = await _productRepository.AddProductWithImagesAsync(dto);

                // Bước 2: SYNCHRONOUS AI Moderation Check (AFTER creation)
                Console.WriteLine($"[PRODUCT SERVICE] Running SYNCHRONOUS AI moderation check for: '{newProduct.Name}'");
                
                ContentModerationResultDTO moderationResult;
                try
                {
                    moderationResult = await _contentModerationService.CheckProductContentAsync(
                        newProduct.Name,
                        newProduct.Description
                    );
                    
                    Console.WriteLine($"[PRODUCT SERVICE] AI Check Result - IsAppropriate: {moderationResult.IsAppropriate}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PRODUCT SERVICE] AI moderation failed: {ex.Message}");
                    
                    // FAIL-SAFE: If AI service is down, set to PENDING for manual review
                    // This ensures NO violated content ever goes live
                    moderationResult = new ContentModerationResultDTO
                    {
                        IsAppropriate = false,
                        Reason = "AI service unavailable - pending manual review",
                        ViolatedTerms = new List<string> { "ai_service_error" }
                    };
                }

                // Bước 3: Set product status based on AI result
                if (!moderationResult.IsAppropriate)
                {
                    // VIOLATED - Set to Pending (customers CANNOT see)
                    await _productRepository.UpdateProductAvailabilityStatusAsync(
                        newProduct.Id, 
                        AvailabilityStatus.pending
                    );
                    Console.WriteLine($"[PRODUCT SERVICE] VIOLATED - Product set to PENDING");
                    Console.WriteLine($"[PRODUCT SERVICE] Reason: {moderationResult.Reason}");
                    Console.WriteLine($"[PRODUCT SERVICE] Violated terms: {string.Join(", ", moderationResult.ViolatedTerms ?? new List<string>())}");
                }
                else
                {
                    // PASSED - Keep as Available (customers can see)
                    Console.WriteLine($"[PRODUCT SERVICE] PASSED - Product remains AVAILABLE");
                }

                Console.WriteLine($"[PRODUCT SERVICE] Product {newProduct.Id} created with status: {newProduct.AvailabilityStatus}");

            // Bước 4: Send chat notification if violated
                if (!moderationResult.IsAppropriate)
                {
                    var productWithProvider = await _productRepository.GetProductWithProviderByIdAsync(newProduct.Id);
                
                if (productWithProvider?.Provider != null)
                {
                    // Lấy Staff ID từ database
                    Guid? defaultStaffId = await GetDefaultStaffIdAsync();
                    
                    // Gửi tin nhắn qua Chat
                    try
                    {
                        var chatResult = await _conversationService.SendViolationMessageToProviderAsync(
                            staffId: defaultStaffId,
                            providerId: productWithProvider.ProviderId,
                            productId: newProduct.Id,
                            productName: newProduct.Name,
                            reason: moderationResult.Reason ?? "Content violates community guidelines",
                            violatedTerms: string.Join(", ", moderationResult.ViolatedTerms ?? new List<string>())
                        );
                        
                        if (chatResult != null)
                        {
                            Console.WriteLine($"[SUCCESS] Violation notification sent via CHAT to Provider {productWithProvider.ProviderId}");
                        }
                        else
                        {
                            Console.WriteLine($"[WARNING] No Staff account found in database - violation notification not sent");
                        }
                    }
                    catch (Exception chatEx)
                    {
                        Console.WriteLine($"[ERROR] Failed to send chat notification: {chatEx.Message}");
                    }
                }
            }

            return _mapper.Map<ProductDTO>(newProduct);
        }

        public async Task<bool> UpdateAsync(ProductDTO productDto)
        {
            // Lấy thông tin product hiện tại
                var existingProduct = await _productRepository.GetProductWithImagesAndProviderAsync(productDto.Id);
                if (existingProduct == null) return false;

            // Nếu có images mới, chỉ xóa những ảnh cũ không còn trong danh sách mới
            if (productDto.Images != null && productDto.Images.Any())
            {
                await DeleteReplacedImagesFromCloudinaryAsync(existingProduct.Images, productDto.Images);
            }

            // Kiểm tra xem name/description có thay đổi không
                var nameChanged = existingProduct.Name != productDto.Name;
                var descriptionChanged = existingProduct.Description != productDto.Description;
            
            Console.WriteLine($"[UPDATE] Product {productDto.Id} - Name changed: {nameChanged} | Description changed: {descriptionChanged}");
            Console.WriteLine($"[UPDATE] Old Name: '{existingProduct.Name}' → New Name: '{productDto.Name}'");
            Console.WriteLine($"[UPDATE] Old Description: '{existingProduct.Description}' → New Description: '{productDto.Description}'");

            // UPDATE NGAY LẬP TỨC
            var updated = await _productRepository.UpdateProductWithImagesAsync(productDto);

            if (updated)
            {
                // LUÔN CHECK MODERATION (synchronous để catch errors)
                var productId = productDto.Id;
                var productName = productDto.Name;
                var productDescription = productDto.Description;
                var providerId = existingProduct.ProviderId;
                
                Console.WriteLine($"[UPDATE] ⚡ Starting SYNCHRONOUS moderation check for Product {productId}");
                
                try
                {
                    Console.WriteLine($"[UPDATE] Calling AI moderation service...");
                    
                                var moderationResult = await _contentModerationService.CheckProductContentAsync(
                                    productName,
                                    productDescription
                                );

                    Console.WriteLine($"[UPDATE] AI Check completed!");
                    Console.WriteLine($"[UPDATE] IsAppropriate: {moderationResult.IsAppropriate}");
                    Console.WriteLine($"[UPDATE] Reason: {moderationResult.Reason}");
                    Console.WriteLine($"[UPDATE] Violated terms: {string.Join(", ", moderationResult.ViolatedTerms ?? new List<string>())}");

                                    if (moderationResult.IsAppropriate)
                                    {
                        Console.WriteLine($"[UPDATE] Product {productId} PASSED moderation");
                        
                        // Nếu product đang PENDING → Set về AVAILABLE
                        if (existingProduct.AvailabilityStatus == AvailabilityStatus.pending)
                        {
                            Console.WriteLine($"[UPDATE] Setting PENDING product back to AVAILABLE");
                            await _productRepository.UpdateProductAvailabilityStatusAsync(
                                productId,
                                AvailabilityStatus.available
                            );
                        }
                                    }
                    else
                    {
                        // VI PHẠM - Set PENDING + Send notification
                        Console.WriteLine($"[UPDATE] Product {productId} VIOLATED content policy!");
                        Console.WriteLine($"[UPDATE] Current status: {existingProduct.AvailabilityStatus} → Setting to PENDING");
                        
                        // Set to PENDING (kể cả khi đang AVAILABLE)
                        await _productRepository.UpdateProductAvailabilityStatusAsync(
                            productId,
                            AvailabilityStatus.pending
                        );
                        
                        Console.WriteLine($"[UPDATE] Product status updated to PENDING. Sending chat notification...");
                        
                        // Get Staff for notification
                        using var scope = _serviceProvider.CreateScope();
                        var scopedDbContext = scope.ServiceProvider.GetRequiredService<ShareItDbContext>();
                        
                        var staff = await scopedDbContext.Users
                            .Where(u => u.Role == BusinessObject.Enums.UserRole.staff)
                            .OrderBy(u => u.CreatedAt)
                            .FirstOrDefaultAsync();
                        
                        if (staff != null)
                        {
                            Console.WriteLine($"[UPDATE] Found Staff account: {staff.Email}. Sending violation notification...");
                            
                            try
                            {
                                await _conversationService.SendViolationMessageToProviderAsync(
                                    staffId: staff.Id,
                                    providerId: providerId,
                                    productId: productId,
                                    productName: productName,
                                    reason: moderationResult.Reason ?? "Content violates community guidelines",
                                    violatedTerms: string.Join(", ", moderationResult.ViolatedTerms ?? new List<string>())
                                );
                                
                                Console.WriteLine($"[UPDATE] Chat notification sent successfully to Provider {providerId}!");
                            }
                            catch (Exception chatEx)
                            {
                                Console.WriteLine($"[UPDATE] ERROR sending chat notification: {chatEx.Message}");
                                Console.WriteLine($"[UPDATE] Chat error stack trace: {chatEx.StackTrace}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[UPDATE] WARNING: No Staff account found in database! Cannot send notification.");
                        }
                    }
                }
                catch (Exception moderationEx)
                {
                    Console.WriteLine($"[UPDATE] MODERATION CHECK FAILED");
                    Console.WriteLine($"[UPDATE] Exception Type: {moderationEx.GetType().Name}");
                    Console.WriteLine($"[UPDATE] Error Message: {moderationEx.Message}");
                    Console.WriteLine($"[UPDATE] Stack Trace: {moderationEx.StackTrace}");
                    
                    if (moderationEx.InnerException != null)
                    {
                        Console.WriteLine($"[UPDATE] Inner Exception: {moderationEx.InnerException.Message}");
                    }
                    
                    // Fail-safe: Nếu AI service lỗi - Set PENDING để admin review thủ công
                    Console.WriteLine($"[UPDATE] Setting product to PENDING as fail-safe due to moderation service failure");
                    await _productRepository.UpdateProductAvailabilityStatusAsync(
                        productId,
                        AvailabilityStatus.pending
                    );
                }

                return true; // Update successful
            }

            return false;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            return await _productRepository.DeleteAsync(id);
        }

        public async Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request)
        {
            return await _productRepository.UpdateProductStatusAsync(request);
        }

        public async Task<bool> HasOrderItemsAsync(Guid productId)
        {
            return await _productRepository.HasOrderItemsAsync(productId);
        }

        /// <summary>
        /// Update product images only - automatically deletes old images from Cloudinary
        /// </summary>
        public async Task<bool> UpdateProductImagesAsync(Guid productId, List<ProductImageDTO> newImages)
        {
            try
            {
                // Get existing product with images
                var existingProduct = await _productRepository.GetProductWithImagesAndProviderAsync(productId);
                if (existingProduct == null) return false;

                // Delete old images from Cloudinary
                await DeleteOldImagesFromCloudinaryAsync(existingProduct.Images);

                // Create updated product DTO with new images only
                var productDto = _mapper.Map<ProductDTO>(existingProduct);
                productDto.Images = newImages;

                // Update in database
                var result = await _productRepository.UpdateProductWithImagesAsync(productDto);
                
                Console.WriteLine($"[PRODUCT SERVICE] Updated images for product {productId}. Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRODUCT SERVICE] Error updating images for product {productId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete old images from Cloudinary
        /// </summary>
        private async Task DeleteOldImagesFromCloudinaryAsync(ICollection<ProductImage> oldImages)
        {
            if (oldImages == null || !oldImages.Any()) return;

            foreach (var oldImage in oldImages)
            {
                var publicId = ExtractPublicIdFromUrl(oldImage.ImageUrl);
                if (!string.IsNullOrEmpty(publicId))
                {
                    try
                    {
                        await _cloudinaryService.DeleteImageAsync(publicId);
                        Console.WriteLine($"[PRODUCT SERVICE] Deleted old image from Cloudinary: {publicId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PRODUCT SERVICE] Failed to delete image {publicId} from Cloudinary: {ex.Message}");
                        // Continue with update even if Cloudinary deletion fails
                    }
                }
            }
        }

        /// <summary>
        /// Delete only replaced images from Cloudinary (smart deletion)
        /// </summary>
        private async Task DeleteReplacedImagesFromCloudinaryAsync(ICollection<ProductImage> oldImages, List<ProductImageDTO> newImages)
        {
            if (oldImages == null || !oldImages.Any()) return;

            // Lấy danh sách URL của ảnh mới
            var newImageUrls = newImages.Select(img => img.ImageUrl).ToHashSet();

            // Chỉ xóa những ảnh cũ không có trong danh sách ảnh mới
            var imagesToDelete = oldImages.Where(oldImg => !newImageUrls.Contains(oldImg.ImageUrl)).ToList();

            Console.WriteLine($"[PRODUCT SERVICE] Found {imagesToDelete.Count} images to delete from Cloudinary out of {oldImages.Count} old images");

            foreach (var imageToDelete in imagesToDelete)
            {
                var publicId = ExtractPublicIdFromUrl(imageToDelete.ImageUrl);
                if (!string.IsNullOrEmpty(publicId))
                {
                    try
                    {
                        await _cloudinaryService.DeleteImageAsync(publicId);
                        Console.WriteLine($"[PRODUCT SERVICE] Deleted replaced image from Cloudinary: {publicId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PRODUCT SERVICE] Failed to delete image {publicId} from Cloudinary: {ex.Message}");
                        // Continue with update even if Cloudinary deletion fails
                    }
                }
            }
        }

        /// <summary>
        /// Extracts Cloudinary public ID from image URL
        /// </summary>
        /// <param name="imageUrl">Full Cloudinary image URL</param>
        /// <returns>Public ID without extension, or null if extraction fails</returns>
        /// <example>
        /// Input: https://res.cloudinary.com/xxx/image/upload/v123/ShareIt/products/user123/image.jpg
        /// Output: ShareIt/products/user123/image
        /// </example>
        private string? ExtractPublicIdFromUrl(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;

            try
            {
                // Cloudinary URL format: .../upload/v[version]/[publicId].[extension]
                var uri = new Uri(imageUrl);
                var path = uri.AbsolutePath;
                
                // Find the upload segment
                var uploadIndex = path.IndexOf("/upload/");
                if (uploadIndex == -1) return null;
                
                // Get everything after /upload/v[version]/
                var afterUpload = path.Substring(uploadIndex + "/upload/".Length);
                
                // Skip version (v123456789)
                var versionEndIndex = afterUpload.IndexOf('/');
                if (versionEndIndex == -1) return null;
                
                var publicIdWithExtension = afterUpload.Substring(versionEndIndex + 1);
                
                // Remove file extension
                var lastDotIndex = publicIdWithExtension.LastIndexOf('.');
                if (lastDotIndex != -1)
                {
                    return publicIdWithExtension.Substring(0, lastDotIndex);
                }
                
                return publicIdWithExtension;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRODUCT SERVICE] Failed to extract public ID from URL: {ex.Message}");
                return null;
            }
        }

        private async Task<Guid?> GetDefaultStaffIdAsync()
        {
            try
            {
                // Lấy Staff đầu tiên từ database (KHÔNG bao gồm Admin)
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShareItDbContext>();
                
                var staff = await dbContext.Users
                    .Where(u => u.Role == BusinessObject.Enums.UserRole.staff)
                    .OrderBy(u => u.CreatedAt) // Lấy staff đầu tiên (oldest)
                    .FirstOrDefaultAsync();
                
                if (staff != null)
                {
                    Console.WriteLine($"[STAFF CONTEXT] Using default Staff: {staff.Id} ({staff.Email})");
                    return staff.Id;
                }
                
                Console.WriteLine("[STAFF CONTEXT] No Staff account found in database");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get default staff ID: {ex.Message}");
                return null;
            }
        }
    }
}
