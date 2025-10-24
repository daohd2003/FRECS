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


namespace Services.ProductServices
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly IContentModerationService _contentModerationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConversationService _conversationService;

        public ProductService(
            IProductRepository productRepository,
            IMapper mapper, 
            IContentModerationService contentModerationService,
            IServiceProvider serviceProvider,
            IConversationService conversationService)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _contentModerationService = contentModerationService;
            _serviceProvider = serviceProvider;
            _conversationService = conversationService;
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
