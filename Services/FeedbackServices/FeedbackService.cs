using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.FeedbackRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Services.ContentModeration;

namespace Services.FeedbackServices
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IFeedbackRepository _feedbackRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IRepository<OrderItem> _orderItemRepo;
        private readonly IRepository<Product> _productRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IMapper _mapper;
        private readonly IContentModerationService _contentModerationService;

        public FeedbackService(
            IFeedbackRepository feedbackRepo,
            IOrderRepository orderRepo,
            IRepository<OrderItem> orderItemRepo,
            IRepository<Product> productRepo,
            IRepository<User> userRepo,
            IMapper mapper,
            IContentModerationService contentModerationService)
        {
            _feedbackRepo = feedbackRepo;
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _productRepo = productRepo;
            _userRepo = userRepo;
            _mapper = mapper;
            _contentModerationService = contentModerationService;
        }

        /// <summary>
        /// Gửi feedback (đánh giá) cho sản phẩm hoặc đơn hàng
        /// Validate: Chỉ cho phép feedback khi đơn hàng đã hoàn thành, không cho feedback trùng
        /// Tự động tính lại rating trung bình của sản phẩm sau khi thêm feedback
        /// </summary>
        /// <param name="dto">Thông tin feedback (TargetType, TargetId, Rating, Comment, OrderItemId)</param>
        /// <param name="customerId">ID khách hàng gửi feedback</param>
        /// <returns>FeedbackResponseDto của feedback vừa tạo</returns>
        /// <exception cref="ArgumentException">Target không hợp lệ hoặc không tồn tại</exception>
        /// <exception cref="UnauthorizedAccessException">User không có quyền feedback đơn hàng này</exception>
        /// <exception cref="InvalidOperationException">Đơn hàng chưa hoàn thành hoặc đã feedback rồi</exception>
        public async Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto dto, Guid customerId)
        {
            Guid? productId = null;
            Guid? orderId = null;
            Guid? orderItemId = null;
            Order order = null;

            // Bước 1: Xử lý feedback cho SẢN PHẨM
            if (dto.TargetType == FeedbackTargetType.Product)
            {
                productId = dto.TargetId;
                // Yêu cầu OrderItemId để biết feedback cho sản phẩm trong đơn hàng nào
                orderItemId = dto.OrderItemId ?? throw new ArgumentException("OrderItemId is required for Product feedback.");

                // Validate OrderItem tồn tại và khớp với ProductId
                var item = await _orderItemRepo.GetByIdAsync(orderItemId.Value);
                if (item == null) throw new ArgumentException("Order item not found.");
                if (item.ProductId != productId.Value) throw new ArgumentException("Product ID does not match the provided Order Item.");

                // Lấy Order để kiểm tra quyền và trạng thái
                order = await _orderRepo.GetByIdAsync(item.OrderId);
                if (order == null) throw new InvalidOperationException("Order associated with this item not found.");
                if (order.CustomerId != customerId) throw new UnauthorizedAccessException("You are not authorized to feedback this order item.");
                
                // Chỉ cho phép feedback khi đơn hàng đã hoàn thành
                // - Thuê: trạng thái returned (đã trả hàng)
                // - Mua: trạng thái in_use (đang sử dụng)
                var validStatusesForFeedback = new[] { OrderStatus.returned, OrderStatus.in_use };
                if (!validStatusesForFeedback.Contains(order.Status))
                {
                    throw new InvalidOperationException("You can only provide feedback for completed orders (in use or returned).");
                }

                // Kiểm tra đã feedback cho OrderItem này chưa (không cho feedback trùng)
                if (await _feedbackRepo.HasUserFeedbackedOrderItemAsync(customerId, orderItemId.Value))
                {
                    throw new InvalidOperationException("You have already submitted feedback for this specific order item.");
                }

                orderId = order.Id; // Liên kết OrderId cho Product feedback
            }
            // Bước 2: Xử lý feedback cho ĐƠN HÀNG
            else if (dto.TargetType == FeedbackTargetType.Order)
            {
                orderId = dto.TargetId;
                order = await _orderRepo.GetByIdAsync(orderId.Value);
                if (order == null) throw new ArgumentException("Order not found.");
                if (order.CustomerId != customerId) throw new UnauthorizedAccessException("You are not authorized to feedback this order.");
                
                // Chỉ cho phép feedback khi đơn hàng đã hoàn thành
                var validStatusesForFeedback = new[] { OrderStatus.returned, OrderStatus.in_use };
                if (!validStatusesForFeedback.Contains(order.Status))
                {
                    throw new InvalidOperationException("You can only provide feedback for completed orders (in use or returned).");
                }

                // Kiểm tra đã feedback cho Order này chưa
                if (await _feedbackRepo.HasUserFeedbackedOrderAsync(customerId, orderId.Value))
                {
                    throw new InvalidOperationException("You have already submitted feedback for this order.");
                }
            }
            else
            {
                throw new ArgumentException("Invalid feedback target type.");
            }

            // Bước 3: Validate sản phẩm tồn tại (nếu là Product feedback)
            if (productId.HasValue)
            {
                var product = await _productRepo.GetByIdAsync(productId.Value);
                if (product == null)
                {
                    throw new ArgumentException("Product not found.");
                }
            }

            // --- Check content moderation ---
            var moderationResult = await _contentModerationService.CheckContentAsync(dto.Comment ?? "");
            var isViolation = !moderationResult.IsAppropriate;
            var violationReason = moderationResult.Reason;
            
            Console.WriteLine($"[MODERATION] Comment: {dto.Comment}");
            Console.WriteLine($"[MODERATION] IsViolation: {isViolation}");
            Console.WriteLine($"[MODERATION] Reason: {violationReason}");
            
            // --- Create Feedback Entity ---
            var feedback = _mapper.Map<Feedback>(dto);

            feedback.TargetType = dto.TargetType;
            feedback.Id = Guid.NewGuid();
            feedback.CustomerId = customerId;
            feedback.CreatedAt = DateTimeHelper.GetVietnamTime();
            feedback.UpdatedAt = null;
            feedback.ProductId = productId;
            feedback.OrderId = orderId;
            feedback.OrderItemId = orderItemId;
            
            // Simplified: If AI detects violation → Block immediately
            if (isViolation)
            {
                Console.WriteLine($"[MODERATION] Blocking feedback {feedback.Id} due to AI detection");
                feedback.IsBlocked = true;
                feedback.IsVisible = false; // Hidden from public
                feedback.ViolationReason = violationReason;
                feedback.BlockedAt = DateTimeHelper.GetVietnamTime();
                // Note: BlockedById is null (auto-blocked by AI, not by staff)
            }
            else
            {
                Console.WriteLine($"[MODERATION] Feedback {feedback.Id} is clean");
                feedback.IsBlocked = false;
                feedback.IsVisible = true;
            }

            // Bước 5: Lưu feedback vào database
            await _feedbackRepo.AddAsync(feedback);

            // Bước 6: Tính lại rating trung bình của sản phẩm
            // Mỗi khi có feedback mới, cập nhật AverageRating của Product
            if (feedback.TargetType == FeedbackTargetType.Product && feedback.ProductId.HasValue)
            {
                await RecalculateProductRatingAsync(feedback.ProductId.Value);
            }

            // Bước 7: Trả về feedback vừa tạo
            var createdFeedback = await _feedbackRepo.GetByIdAsync(feedback.Id);
            return _mapper.Map<FeedbackResponseDto>(createdFeedback);
        }

        /// <summary>
        /// Lấy thông tin chi tiết một feedback theo ID
        /// </summary>
        /// <param name="feedbackId">ID feedback cần lấy</param>
        /// <returns>FeedbackResponseDto</returns>
        /// <exception cref="KeyNotFoundException">Feedback không tồn tại</exception>
        public async Task<FeedbackResponseDto> GetFeedbackByIdAsync(Guid feedbackId)
        {
            // Repository đã include Customer, Product, Order để lấy đầy đủ thông tin
            var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedback == null) throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");
            return _mapper.Map<FeedbackResponseDto>(feedback);
        }

        /// <summary>
        /// Lấy tất cả feedback cho một target cụ thể (Product hoặc Order)
        /// Dùng để hiển thị danh sách đánh giá trên trang product detail hoặc order detail
        /// </summary>
        /// <param name="targetType">Loại target (Product hoặc Order)</param>
        /// <param name="targetId">ID của target</param>
        /// <returns>Danh sách FeedbackResponseDto</returns>
        public async Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByTargetAsync(FeedbackTargetType targetType, Guid targetId)
        {
            var feedbacks = await _feedbackRepo.GetFeedbacksByTargetAsync(targetType, targetId);
            return _mapper.Map<IEnumerable<FeedbackResponseDto>>(feedbacks);
        }

        /// <summary>
        /// Lấy tất cả feedback mà customer đã gửi
        /// Dùng để hiển thị lịch sử đánh giá của customer
        /// </summary>
        /// <param name="customerId">ID khách hàng</param>
        /// <returns>Danh sách FeedbackResponseDto</returns>
        public async Task<IEnumerable<FeedbackResponseDto>> GetCustomerFeedbacksAsync(Guid customerId)
        {
            var feedbacks = await _feedbackRepo.GetFeedbacksByCustomerIdAsync(customerId);
            return _mapper.Map<IEnumerable<FeedbackResponseDto>>(feedbacks);
        }

        /// <summary>
        /// Lấy tất cả feedback của một customer cụ thể cho một sản phẩm
        /// Dùng cho Provider xem feedback của customer đã mua sản phẩm
        /// </summary>
        /// <param name="productId">ID sản phẩm</param>
        /// <param name="customerId">ID customer</param>
        /// <returns>Danh sách FeedbackResponseDto</returns>
        public async Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByProductAndCustomerAsync(Guid productId, Guid customerId)
        {
            var feedbacks = await _feedbackRepo.GetFeedbacksByProductAndCustomerAsync(productId, customerId);
            return _mapper.Map<IEnumerable<FeedbackResponseDto>>(feedbacks);
        }

        public async Task<IEnumerable<FeedbackResponseDto>> GetFeedbacksByProviderIdAsync(Guid providerId, Guid currentUserId, bool isAdmin)
        {
            if (!isAdmin && providerId != currentUserId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view feedbacks of other providers.");
            }

            var providerUser = await _userRepo.GetByIdAsync(providerId);
            if (providerUser == null || (providerUser.Role != UserRole.provider && !isAdmin))
            {
                throw new ArgumentException("Provided ID does not belong to a valid provider.");
            }

            var feedbacks = await _feedbackRepo.GetFeedbacksByProviderIdAsync(providerId);

            return _mapper.Map<IEnumerable<FeedbackResponseDto>>(feedbacks);
        }

        // --- UPDATE ---
        public async Task UpdateFeedbackAsync(Guid feedbackId, FeedbackRequestDto dto, Guid currentUserId)
        {
            var feedbackToUpdate = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedbackToUpdate == null) throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");

            if (feedbackToUpdate.CustomerId != currentUserId && !(await IsUserAdminAsync(currentUserId)))
            {
                throw new UnauthorizedAccessException("You are not authorized to update this feedback.");
            }

            // --- Validation for Update ---
            if (feedbackToUpdate.TargetType != dto.TargetType)
            {
                throw new InvalidOperationException("Cannot change feedback target type when updating feedback.");
            }

            // Check if TargetId matches original entity's ProductId/OrderId
            if (dto.TargetType == FeedbackTargetType.Product)
            {
                if (feedbackToUpdate.ProductId != dto.TargetId)
                {
                    throw new InvalidOperationException("Product ID mismatch for update. Cannot change product associated with this feedback.");
                }
                if (feedbackToUpdate.OrderItemId != dto.OrderItemId)
                {
                    throw new InvalidOperationException("OrderItem ID mismatch for update. Cannot change order item associated with this product feedback.");
                }
            }
            else if (dto.TargetType == FeedbackTargetType.Order)
            {
                if (feedbackToUpdate.OrderId != dto.TargetId)
                {
                    throw new InvalidOperationException("Order ID mismatch for update. Cannot change order associated with this feedback.");
                }
            }

            feedbackToUpdate.Rating = dto.Rating;
            feedbackToUpdate.Comment = dto.Comment;
            feedbackToUpdate.UpdatedAt = DateTime.UtcNow;

            await _feedbackRepo.UpdateAsync(feedbackToUpdate);

            // --- Recalculate Product Rating ---
            if (feedbackToUpdate.TargetType == FeedbackTargetType.Product && feedbackToUpdate.ProductId.HasValue)
            {
                await RecalculateProductRatingAsync(feedbackToUpdate.ProductId.Value);
            }
        }

        // --- DELETE ---
        public async Task DeleteFeedbackAsync(Guid feedbackId, Guid currentUserId)
        {
            var feedbackToDelete = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedbackToDelete == null) throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");

            if (feedbackToDelete.CustomerId != currentUserId && !(await IsUserAdminAsync(currentUserId)))
            {
                throw new UnauthorizedAccessException("You are not authorized to delete this feedback.");
            }

            await _feedbackRepo.DeleteAsync(feedbackId);

            // --- Recalculate Product Rating ---
            if (feedbackToDelete.TargetType == FeedbackTargetType.Product && feedbackToDelete.ProductId.HasValue)
            {
                await RecalculateProductRatingAsync(feedbackToDelete.ProductId.Value);
            }
        }

        // --- BỔ SUNG: SUBMIT PROVIDER RESPONSE ---
        public async Task SubmitProviderResponseAsync(Guid feedbackId, SubmitProviderResponseDto responseDto, Guid providerOrAdminId)
        {
            var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedback == null)
            {
                throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");
            }

            // Kiểm tra quyền của người phản hồi
            var responder = await _userRepo.GetByIdAsync(providerOrAdminId);
            if (responder == null || (responder.Role != UserRole.provider && responder.Role != UserRole.admin))
            {
                throw new UnauthorizedAccessException("Only providers or administrators can respond to feedback.");
            }

            // Provider chỉ được phản hồi feedback của sản phẩm/order của mình
            if (responder.Role == UserRole.provider)
            {
                bool authorizedProvider = false;
                if (feedback.TargetType == FeedbackTargetType.Product && feedback.Product != null && feedback.Product.ProviderId == providerOrAdminId)
                {
                    authorizedProvider = true;
                }
                else if (feedback.TargetType == FeedbackTargetType.Order && feedback.Order != null && feedback.Order.ProviderId == providerOrAdminId)
                {
                    authorizedProvider = true;
                }

                if (!authorizedProvider)
                {
                    throw new UnauthorizedAccessException("You can only respond to feedback for your own products or orders.");
                }
            }

            feedback.ProviderResponse = responseDto.ResponseContent;
            feedback.ProviderResponseAt = DateTime.UtcNow;
            feedback.ProviderResponseById = providerOrAdminId;

            await _feedbackRepo.UpdateAsync(feedback);
        }

        /// <summary>
        /// Update provider response to existing feedback
        /// Only the provider who owns the product/order or admin can update
        /// </summary>
        public async Task UpdateProviderResponseAsync(Guid feedbackId, UpdateProviderResponseDto dto, Guid providerOrAdminId)
        {
            var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedback == null)
            {
                throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");
            }

            // Check if feedback has provider response
            if (string.IsNullOrEmpty(feedback.ProviderResponse))
            {
                throw new InvalidOperationException("This feedback does not have a provider response yet. Use submit response instead.");
            }

            // Check user permissions
            var responder = await _userRepo.GetByIdAsync(providerOrAdminId);
            if (responder == null || (responder.Role != UserRole.provider && responder.Role != UserRole.admin))
            {
                throw new UnauthorizedAccessException("Only providers or administrators can update feedback responses.");
            }

            // Provider can only update their own response
            if (responder.Role == UserRole.provider)
            {
                bool authorizedProvider = false;
                if (feedback.TargetType == FeedbackTargetType.Product && feedback.Product != null && feedback.Product.ProviderId == providerOrAdminId)
                {
                    authorizedProvider = true;
                }
                else if (feedback.TargetType == FeedbackTargetType.Order && feedback.Order != null && feedback.Order.ProviderId == providerOrAdminId)
                {
                    authorizedProvider = true;
                }

                if (!authorizedProvider)
                {
                    throw new UnauthorizedAccessException("You can only update responses for your own products or orders.");
                }
            }

            // Check content moderation before updating
            var moderationResult = await _contentModerationService.CheckFeedbackContentAsync(dto.ResponseContent);
            
            if (!moderationResult.IsAppropriate)
            {
                throw new InvalidOperationException($"Response content violates community guidelines: {moderationResult.Reason}");
            }

            // Update response
            feedback.ProviderResponse = dto.ResponseContent;
            feedback.ProviderResponseAt = DateTime.UtcNow; // Update timestamp
            feedback.ProviderResponseById = providerOrAdminId; // Update responder ID

            await _feedbackRepo.UpdateAsync(feedback);
        }

        /// <summary>
        /// Update customer feedback (rating and comment only)
        /// Only the customer who created the feedback or admin can update
        /// </summary>
        public async Task UpdateCustomerFeedbackAsync(Guid feedbackId, UpdateFeedbackDto dto, Guid customerId)
        {
            var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
            if (feedback == null)
            {
                throw new KeyNotFoundException($"Feedback with ID {feedbackId} not found.");
            }

            // Check if user is the owner or admin
            var isAdmin = await IsUserAdminAsync(customerId);
            if (feedback.CustomerId != customerId && !isAdmin)
            {
                throw new UnauthorizedAccessException("You are not authorized to update this feedback.");
            }

            var oldRating = feedback.Rating;
            var wasBlocked = feedback.IsBlocked;
            var wasInvisible = !feedback.IsVisible;

            // Check content moderation - always check if comment exists (even if not changed)
            var commentToCheck = !string.IsNullOrWhiteSpace(dto.Comment) ? dto.Comment : feedback.Comment;
            
            if (!string.IsNullOrWhiteSpace(commentToCheck))
            {
                var moderationResult = await _contentModerationService.CheckFeedbackContentAsync(commentToCheck);
                
                if (!moderationResult.IsAppropriate)
                {
                    // Content violates guidelines - keep/set as blocked
                    feedback.IsBlocked = true;
                    feedback.IsFlagged = true;
                    feedback.IsVisible = false; // Hide from public
                    feedback.ViolationReason = moderationResult.Reason;
                    
                    if (!wasBlocked)
                    {
                        feedback.BlockedAt = DateTime.UtcNow;
                        feedback.BlockedById = null; // Auto-blocked by system
                    }
                    
                    throw new InvalidOperationException($"Feedback content violates community guidelines: {moderationResult.Reason}");
                }
                else
                {
                    // Content is appropriate - unblock and make visible
                    if (wasBlocked || wasInvisible)
                    {
                        feedback.IsBlocked = false;
                        feedback.IsFlagged = false;
                        feedback.IsVisible = true; // Make feedback visible to everyone
                        feedback.ViolationReason = null;
                        feedback.BlockedAt = null;
                        feedback.BlockedById = null;
                    }
                }
            }

            // Update feedback
            feedback.Rating = dto.Rating;
            feedback.Comment = dto.Comment;
            feedback.UpdatedAt = DateTime.UtcNow;

            await _feedbackRepo.UpdateAsync(feedback);

            // Recalculate product rating if rating changed and feedback is for a product
            if (oldRating != dto.Rating && feedback.TargetType == FeedbackTargetType.Product && feedback.ProductId.HasValue)
            {
                await RecalculateProductRatingAsync(feedback.ProductId.Value);
            }
        }

        // --- INTERNAL HELPER FUNCTIONS ---
        public async Task RecalculateProductRatingAsync(Guid productId)
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null) return;

            var allProductFeedbacks = await _feedbackRepo.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId);

            if (allProductFeedbacks != null && allProductFeedbacks.Any())
            {
                product.AverageRating = (decimal)allProductFeedbacks.Average(f => f.Rating);
                product.RatingCount = allProductFeedbacks.Count();
            }
            else
            {
                product.AverageRating = 0.0m;
                product.RatingCount = 0;
            }
            await _productRepo.UpdateAsync(product);
        }

        private async Task<bool> IsUserAdminAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            return user?.Role == UserRole.admin;
        }
        // Overload for backward compatibility with tests
        public async Task<ApiResponse<PaginatedResponse<FeedbackResponseDto>>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize)
        {
            return await GetFeedbacksByProductAsync(productId, page, pageSize, null);
        }
        
        // For Staff/Admin - return ALL feedbacks without filtering
        public async Task<ApiResponse<PaginatedResponse<FeedbackResponseDto>>> GetAllFeedbacksByProductForStaffAsync(Guid productId, int page, int pageSize)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Invalid page or pageSize", null);
            }

            var paginatedFeedbacks = await _feedbackRepo.GetFeedbacksByProductAsync(productId, page, pageSize);
            var responseDtos = _mapper.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items);
            
            // Map moderation fields
            for (int i = 0; i < paginatedFeedbacks.Items.Count; i++)
            {
                responseDtos[i].IsBlocked = paginatedFeedbacks.Items[i].IsBlocked;
                responseDtos[i].IsVisible = paginatedFeedbacks.Items[i].IsVisible;
                responseDtos[i].ViolationReason = paginatedFeedbacks.Items[i].ViolationReason;
            }

            var paginatedDtoResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = responseDtos,
                Page = paginatedFeedbacks.Page,
                PageSize = paginatedFeedbacks.PageSize,
                TotalItems = paginatedFeedbacks.TotalItems
            };

            return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Success", paginatedDtoResponse);
        }

        public async Task<ApiResponse<PaginatedResponse<FeedbackResponseDto>>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize, Guid? currentUserId)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Invalid page or pageSize", null);
            }

            var paginatedFeedbacks = await _feedbackRepo.GetFeedbacksByProductAsync(productId, page, pageSize);

            // Check if current user is the provider of this product
            bool isProvider = false;
            if (currentUserId.HasValue && paginatedFeedbacks.Items.Any())
            {
                var firstFeedback = paginatedFeedbacks.Items.First();
                if (firstFeedback.Product?.ProviderId == currentUserId.Value)
                {
                    isProvider = true;
                }
            }

            // Filter feedbacks based on visibility rules
            var filteredFeedbacks = paginatedFeedbacks.Items.Where(f =>
            {
                // Always show visible feedbacks
                if (f.IsVisible) return true;
                
                // Provider sees ALL feedbacks (including blocked)
                if (isProvider) return true;
                
                // Show blocked feedbacks only to the author
                if (!f.IsVisible && currentUserId.HasValue && f.CustomerId == currentUserId.Value)
                {
                    return true;
                }
                
                // Hide from everyone else
                return false;
            }).ToList();

            var responseDtos = _mapper.Map<List<FeedbackResponseDto>>(filteredFeedbacks);
            
            // Map moderation fields manually
            for (int i = 0; i < filteredFeedbacks.Count; i++)
            {
                responseDtos[i].IsBlocked = filteredFeedbacks[i].IsBlocked;
                responseDtos[i].IsVisible = filteredFeedbacks[i].IsVisible;
                
                // Show ViolationReason only to the author
                if (!filteredFeedbacks[i].IsVisible && 
                    currentUserId.HasValue && 
                    filteredFeedbacks[i].CustomerId == currentUserId.Value)
                {
                    responseDtos[i].ViolationReason = filteredFeedbacks[i].ViolationReason;
                }
            }

            var paginatedDtoResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = responseDtos,
                Page = paginatedFeedbacks.Page,
                PageSize = paginatedFeedbacks.PageSize,
                TotalItems = filteredFeedbacks.Count // Use filtered count
            };

            return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Success", paginatedDtoResponse);
        }

        // Feedback Management Methods
        public async Task<ApiResponse<PaginatedResponse<FeedbackManagementDto>>> GetAllFeedbacksAsync(FeedbackFilterDto filter)
        {
            try
            {
                var paginatedFeedbacks = await _feedbackRepo.GetAllFeedbacksWithFilterAsync(filter);

                var managementDtos = paginatedFeedbacks.Items.Select(f => new FeedbackManagementDto
                {
                    FeedbackId = f.Id,
                    ProductId = f.ProductId,
                    ProductName = f.Product?.Name,
                    ProductImageUrl = f.Product?.Images?.FirstOrDefault()?.ImageUrl,
                    ProductPrice = f.Product?.PricePerDay,
                    ProviderName = f.Product?.Provider?.Profile?.FullName,
                    CustomerId = f.CustomerId,
                    CustomerName = f.Customer?.Profile?.FullName ?? "Unknown",
                    CustomerEmail = f.Customer?.Email,
                    CustomerProfilePicture = f.Customer?.Profile?.ProfilePictureUrl,
                    Rating = f.Rating,
                    Comment = f.Comment,
                    CreatedAt = f.CreatedAt,
                    ProviderResponse = f.ProviderResponse,
                    ProviderResponseAt = f.ProviderResponseAt,
                    ProviderResponderName = f.ProviderResponder?.Profile?.FullName,
                    IsBlocked = f.IsBlocked,
                    IsVisible = f.IsVisible,
                    BlockedAt = f.BlockedAt,
                    BlockedByName = f.BlockedBy?.Profile?.FullName,
                    Status = GetFeedbackStatus(f)
                }).ToList();

                var response = new PaginatedResponse<FeedbackManagementDto>
                {
                    Items = managementDtos,
                    Page = paginatedFeedbacks.Page,
                    PageSize = paginatedFeedbacks.PageSize,
                    TotalItems = paginatedFeedbacks.TotalItems
                };

                return new ApiResponse<PaginatedResponse<FeedbackManagementDto>>("Success", response);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<FeedbackManagementDto>>($"Error: {ex.Message}", null);
            }
        }

        public async Task<ApiResponse<FeedbackDetailDto>> GetFeedbackDetailAsync(Guid feedbackId)
        {
            try
            {
                var feedback = await _feedbackRepo.GetFeedbackDetailAsync(feedbackId);
                if (feedback == null)
                    return new ApiResponse<FeedbackDetailDto>("Feedback not found", null);

                // Get OrderItem information if available
                OrderItemInfoDto? orderItemInfo = null;
                if (feedback.OrderItemId.HasValue)
                {
                    var orderItem = await _orderItemRepo.GetByIdAsync(feedback.OrderItemId.Value);
                    if (orderItem != null)
                    {
                        decimal totalPrice = orderItem.TransactionType == TransactionType.rental
                            ? orderItem.DailyRate * (orderItem.RentalDays ?? 1) * orderItem.Quantity
                            : orderItem.DailyRate * orderItem.Quantity;

                        orderItemInfo = new OrderItemInfoDto
                        {
                            OrderItemId = orderItem.Id,
                            OrderId = orderItem.OrderId,
                            TransactionType = orderItem.TransactionType.ToString(),
                            Quantity = orderItem.Quantity,
                            RentalDays = orderItem.RentalDays,
                            DailyRate = orderItem.DailyRate,
                            DepositPerUnit = orderItem.DepositPerUnit,
                            TotalPrice = totalPrice
                        };
                    }
                }

                var detailDto = new FeedbackDetailDto
                {
                    FeedbackId = feedback.Id,
                    Product = feedback.Product != null ? new ProductInfoDto
                    {
                        ProductId = feedback.Product.Id,
                        ProductName = feedback.Product.Name,
                        Description = feedback.Product.Description,
                        PricePerDay = feedback.Product.PricePerDay,
                        PurchasePrice = feedback.Product.PurchasePrice,
                        RentalStatus = feedback.Product.RentalStatus.ToString(),
                        PurchaseStatus = feedback.Product.PurchaseStatus.ToString(),
                        RentalQuantity = feedback.Product.RentalQuantity,
                        PurchaseQuantity = feedback.Product.PurchaseQuantity,
                        ImageUrl = feedback.Product.Images?.FirstOrDefault()?.ImageUrl,
                        ProviderName = feedback.Product.Provider?.Profile?.FullName ?? "Unknown",
                        ProviderEmail = feedback.Product.Provider?.Email,
                        AverageRating = (double)feedback.Product.AverageRating,
                        TotalReviews = feedback.Product.RatingCount
                    } : null,
                    OrderItem = orderItemInfo,
                    Customer = new CustomerInfoDto
                    {
                        CustomerId = feedback.CustomerId,
                        CustomerName = feedback.Customer?.Profile?.FullName ?? "Unknown",
                        Email = feedback.Customer?.Email,
                        ProfilePicture = feedback.Customer?.Profile?.ProfilePictureUrl,
                        SubmittedAt = feedback.CreatedAt
                    },
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    CreatedAt = feedback.CreatedAt,
                    UpdatedAt = feedback.UpdatedAt,
                    ProviderResponse = feedback.ProviderResponse != null ? new ProviderResponseInfoDto
                    {
                        ResponseText = feedback.ProviderResponse,
                        ResponderName = feedback.ProviderResponder?.Profile?.FullName ?? "Unknown",
                        RespondedAt = feedback.ProviderResponseAt ?? DateTime.UtcNow
                    } : null,
                    Status = new StatusInfoDto
                    {
                        Visibility = feedback.IsVisible ? "Visible to public" : "Hidden from public",
                        ContentStatus = feedback.IsBlocked ? "Blocked content" : "Clear content",
                        ResponseStatus = feedback.ProviderResponse != null ? "Responded" : "No response",
                        IsBlocked = feedback.IsBlocked,
                        BlockedAt = feedback.BlockedAt,
                        BlockedByName = feedback.BlockedBy?.Profile?.FullName
                    }
                };

                return new ApiResponse<FeedbackDetailDto>("Success", detailDto);
            }
            catch (Exception ex)
            {
                return new ApiResponse<FeedbackDetailDto>($"Error: {ex.Message}", null);
            }
        }

        public async Task<ApiResponse<bool>> BlockFeedbackAsync(Guid feedbackId, Guid staffId)
        {
            try
            {
                var result = await _feedbackRepo.BlockFeedbackAsync(feedbackId, staffId);
                if (!result)
                    return new ApiResponse<bool>("Feedback not found", false);

                // Recalculate product rating after blocking
                var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
                if (feedback?.ProductId != null)
                {
                    await RecalculateProductRatingAsync(feedback.ProductId.Value);
                }

                return new ApiResponse<bool>("Feedback blocked successfully", true);
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>($"Error: {ex.Message}", false);
            }
        }

        public async Task<ApiResponse<bool>> UnblockFeedbackAsync(Guid feedbackId)
        {
            try
            {
                var result = await _feedbackRepo.UnblockFeedbackAsync(feedbackId);
                if (!result)
                    return new ApiResponse<bool>("Feedback not found", false);

                // Clear violation info when unblocking
                var feedback = await _feedbackRepo.GetByIdAsync(feedbackId);
                if (feedback != null)
                {
                    feedback.IsVisible = true;
                    feedback.ViolationReason = null;
                    await _feedbackRepo.UpdateAsync(feedback);
                    
                    // Recalculate product rating after unblocking
                    if (feedback.ProductId != null)
                    {
                        await RecalculateProductRatingAsync(feedback.ProductId.Value);
                    }
                }

                return new ApiResponse<bool>("Feedback unblocked successfully", true);
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>($"Error: {ex.Message}", false);
            }
        }

        public async Task<ApiResponse<FeedbackStatisticsDto>> GetFeedbackStatisticsAsync()
        {
            try
            {
                var stats = await _feedbackRepo.GetFeedbackStatisticsAsync();
                return new ApiResponse<FeedbackStatisticsDto>("Success", stats);
            }
            catch (Exception ex)
            {
                return new ApiResponse<FeedbackStatisticsDto>($"Error: {ex.Message}", null);
            }
        }

        private string GetFeedbackStatus(Feedback feedback)
        {
            if (feedback.IsBlocked) return "Blocked";
            if (feedback.ProviderResponse != null) return "Responded";
            return "Active";
        }
    }
}
