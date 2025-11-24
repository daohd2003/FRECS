using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.FeedbackRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;

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

        public FeedbackService(
            IFeedbackRepository feedbackRepo,
            IOrderRepository orderRepo,
            IRepository<OrderItem> orderItemRepo,
            IRepository<Product> productRepo,
            IRepository<User> userRepo,
            IMapper mapper)
        {
            _feedbackRepo = feedbackRepo;
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _productRepo = productRepo;
            _userRepo = userRepo;
            _mapper = mapper;
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

            // Bước 4: Tạo Feedback entity
            var feedback = _mapper.Map<Feedback>(dto);

            feedback.TargetType = dto.TargetType;
            feedback.Id = Guid.NewGuid();
            feedback.CustomerId = customerId;
            feedback.CreatedAt = DateTimeHelper.GetVietnamTime();
            feedback.UpdatedAt = null;
            feedback.ProductId = productId;
            feedback.OrderId = orderId;
            feedback.OrderItemId = orderItemId;

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
        public async Task<ApiResponse<PaginatedResponse<FeedbackResponseDto>>> GetFeedbacksByProductAsync(Guid productId, int page, int pageSize)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Invalid page or pageSize", null);
            }

            var paginatedFeedbacks = await _feedbackRepo.GetFeedbacksByProductAsync(productId, page, pageSize);

            var responseDtos = _mapper.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items);

            var paginatedDtoResponse = new PaginatedResponse<FeedbackResponseDto>
            {
                Items = responseDtos,
                Page = paginatedFeedbacks.Page,
                PageSize = paginatedFeedbacks.PageSize,
                TotalItems = paginatedFeedbacks.TotalItems
            };

            return new ApiResponse<PaginatedResponse<FeedbackResponseDto>>("Success", paginatedDtoResponse);
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
    }
}
