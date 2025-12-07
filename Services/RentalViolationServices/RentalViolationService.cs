using AutoMapper;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.OrderRepositories;
using Repositories.ProductRepositories;
using Repositories.RentalViolationRepositories;
using Services.CloudServices;
using Services.NotificationServices;

namespace Services.RentalViolationServices
{
    public class RentalViolationService : IRentalViolationService
    {
        private readonly IRentalViolationRepository _violationRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IProductRepository _productRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;
        private readonly ShareItDbContext _context;

        public RentalViolationService(
            IRentalViolationRepository violationRepo,
            IOrderRepository orderRepo,
            IProductRepository productRepository,
            ICloudinaryService cloudinaryService,
            INotificationService notificationService,
            IMapper mapper,
            ShareItDbContext context)
        {
            _violationRepo = violationRepo;
            _orderRepo = orderRepo;
            _productRepository = productRepository;
            _cloudinaryService = cloudinaryService;
            _notificationService = notificationService;
            _mapper = mapper;
            _context = context;
        }

        public async Task<List<Guid>> CreateMultipleViolationsAsync(CreateMultipleViolationsRequestDto dto, Guid providerId)
        {
            var createdViolationIds = new List<Guid>();

            // Verify order belongs to this provider
            var order = await _orderRepo.GetOrderWithItemsAsync(dto.OrderId);
            if (order == null)
            {
                throw new Exception("Order not found");
            }

            if (order.ProviderId != providerId)
            {
                throw new UnauthorizedAccessException("You do not have permission to access this order");
            }

            // Validate all files first before creating any violations
            foreach (var violationDto in dto.Violations)
            {
                if (violationDto.EvidenceFiles != null && violationDto.EvidenceFiles.Any())
                {
                    foreach (var file in violationDto.EvidenceFiles)
                    {
                        var extension = Path.GetExtension(file.FileName)?.ToLower() ?? string.Empty;
                        var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(extension);
                        var isVideo = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv", ".wmv" }.Contains(extension);

                        if (!isImage && !isVideo)
                        {
                            throw new ArgumentException($"File '{file.FileName}' has invalid format. Only images (JPG, PNG, GIF, WebP, BMP) or videos (MP4, MOV, AVI, MKV, WebM, FLV, WMV) are accepted.");
                        }

                        // Validate file size
                        var maxSize = isImage ? 10 * 1024 * 1024 : 100 * 1024 * 1024;
                        if (file.Length > maxSize)
                        {
                            var maxSizeMB = isImage ? 10 : 100;
                            throw new ArgumentException($"File '{file.FileName}' exceeds the allowed size of {maxSizeMB}MB. Size: {file.Length / 1024 / 1024}MB");
                        }
                    }
                }
            }

            // Use a list to store uploaded file URLs for cleanup if needed
            var uploadedFilePublicIds = new List<string>();

            try
            {
                foreach (var violationDto in dto.Violations)
                {
                    // Verify OrderItem belongs to this Order
                    var orderItem = order.Items.FirstOrDefault(i => i.Id == violationDto.OrderItemId);
                    if (orderItem == null)
                    {
                        throw new Exception($"Product {violationDto.OrderItemId} does not belong to this order");
                    }

                    // Check if this OrderItem already has a violation report
                    var existingViolations = await _violationRepo.GetViolationsByOrderItemIdAsync(violationDto.OrderItemId);
                    if (existingViolations.Any())
                    {
                        // Skip this item if it already has violations (edit mode)
                        // Frontend should handle filtering, but this provides backend safety
                        continue;
                    }

                    // Create RentalViolation
                    var violation = new RentalViolation
                    {
                        ViolationId = Guid.NewGuid(),
                        OrderItemId = violationDto.OrderItemId,
                        ViolationType = violationDto.ViolationType,
                        Description = violationDto.Description,
                        DamagePercentage = violationDto.DamagePercentage,
                        PenaltyPercentage = violationDto.PenaltyPercentage,
                        PenaltyAmount = violationDto.PenaltyAmount,
                        Status = ViolationStatus.PENDING,
                        CreatedAt = DateTimeHelper.GetVietnamTime()
                    };

                    await _violationRepo.AddAsync(violation);
                    createdViolationIds.Add(violation.ViolationId);

                    // Upload evidence images/videos in parallel for better performance
                    if (violationDto.EvidenceFiles != null && violationDto.EvidenceFiles.Any())
                    {
                        var uploadTasks = violationDto.EvidenceFiles.Select(async file =>
                        {
                            // Upload to Cloudinary
                            var uploadResult = await _cloudinaryService.UploadMediaFileAsync(
                                file,
                                providerId,
                                "ShareIt",
                                "violations"
                            );

                            // Track uploaded files for potential cleanup
                            lock (uploadedFilePublicIds)
                            {
                                uploadedFilePublicIds.Add(uploadResult.PublicId);
                            }

                            // Determine file type
                            string fileType = file.ContentType.StartsWith("image/") ? "image" : "video";

                            // Save to database
                            var image = new RentalViolationImage
                            {
                                ImageId = Guid.NewGuid(),
                                ViolationId = violation.ViolationId,
                                ImageUrl = uploadResult.ImageUrl,
                                UploadedBy = EvidenceUploadedBy.PROVIDER,
                                FileType = fileType,
                                UploadedAt = DateTimeHelper.GetVietnamTime()
                            };

                            await _violationRepo.AddEvidenceImageAsync(image);
                        });

                        // Wait for all uploads to complete
                        await Task.WhenAll(uploadTasks);
                    }
                }

                // Update order status to returned_with_issue if currently returning
                if (createdViolationIds.Any() && order.Status == OrderStatus.returning)
                {
                    order.Status = OrderStatus.returned_with_issue;
                    await _orderRepo.UpdateAsync(order);
                }

                // Send notification to customer after all violations are created
                if (createdViolationIds.Any())
                {
                    var violationCount = createdViolationIds.Count;
                    var itemText = violationCount > 1 ? "items" : "item";
                    var message = $"⚠️ Violation Report: {violationCount} {itemText} from your order have been reported with issues. Please review and respond.";

                    await _notificationService.SendNotification(
                        order.CustomerId,
                        message,
                        NotificationType.order,
                        order.Id
                    );
                }

                return createdViolationIds;
            }
            catch (Exception)
            {
                // Clean up uploaded files from Cloudinary
                foreach (var publicId in uploadedFilePublicIds)
                {
                    try
                    {
                        await _cloudinaryService.DeleteImageAsync(publicId);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                // Re-throw the original exception
                throw;
            }
        }

        public async Task<RentalViolationDetailDto?> GetViolationDetailAsync(Guid violationId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return null;

            // Calculate deposit amount and refund amount
            var depositAmount = violation.OrderItem.DepositPerUnit * violation.OrderItem.Quantity;
            var refundAmount = depositAmount - violation.PenaltyAmount;

            var detailDto = new RentalViolationDetailDto
            {
                ViolationId = violation.ViolationId,
                OrderItemId = violation.OrderItemId,
                ViolationType = violation.ViolationType,
                ViolationTypeDisplay = GetViolationTypeDisplay(violation.ViolationType),
                Description = violation.Description,
                DamagePercentage = violation.DamagePercentage,
                PenaltyPercentage = violation.PenaltyPercentage,
                PenaltyAmount = violation.PenaltyAmount,
                DepositAmount = depositAmount,
                RefundAmount = refundAmount,
                Status = violation.Status,
                StatusDisplay = GetViolationStatusDisplay(violation.Status),
                CustomerNotes = violation.CustomerNotes,
                CustomerResponseAt = violation.CustomerResponseAt,
                ProviderResponseToCustomer = violation.ProviderResponseToCustomer,
                ProviderResponseAt = violation.ProviderResponseAt,
                CreatedAt = violation.CreatedAt,
                UpdatedAt = violation.UpdatedAt,
                OrderItem = _mapper.Map<BusinessObject.DTOs.OrdersDto.OrderItemDetailsDto>(violation.OrderItem),
                Images = violation.Images.Select(img => new RentalViolationImageDto
                {
                    ImageId = img.ImageId,
                    ViolationId = img.ViolationId,
                    ImageUrl = img.ImageUrl,
                    UploadedBy = img.UploadedBy,
                    UploadedByDisplay = img.UploadedBy == EvidenceUploadedBy.PROVIDER ? "Nhà cung cấp" : "Khách hàng",
                    FileType = img.FileType,
                    UploadedAt = img.UploadedAt
                }).ToList()
            };

            return detailDto;
        }

        public async Task<IEnumerable<RentalViolationDto>> GetViolationsByOrderIdAsync(Guid orderId)
        {
            var violations = await _violationRepo.GetViolationsByOrderIdAsync(orderId);
            var violationDtos = _mapper.Map<IEnumerable<RentalViolationDto>>(violations).ToList();

            // Populate evidence URLs for each violation
            foreach (var dto in violationDtos)
            {
                var violation = violations.First(v => v.ViolationId == dto.ViolationId);
                if (violation.Images != null && violation.Images.Any())
                {
                    dto.EvidenceUrls = violation.Images
                        .Where(img => img.UploadedBy == BusinessObject.Enums.EvidenceUploadedBy.PROVIDER)
                        .Select(img => img.ImageUrl)
                        .ToList();
                    dto.EvidenceCount = dto.EvidenceUrls.Count;
                }
            }

            return violationDtos;
        }

        public async Task<IEnumerable<RentalViolationDetailDto>> GetViolationsWithDetailsByOrderIdAsync(Guid orderId)
        {
            var violations = await _violationRepo.GetViolationsByOrderIdAsync(orderId);
            var detailDtos = new List<RentalViolationDetailDto>();

            foreach (var violation in violations)
            {
                // Calculate deposit amount and refund amount
                var depositAmount = violation.OrderItem.DepositPerUnit * violation.OrderItem.Quantity;
                var refundAmount = depositAmount - violation.PenaltyAmount;

                var detailDto = new RentalViolationDetailDto
                {
                    ViolationId = violation.ViolationId,
                    OrderItemId = violation.OrderItemId,
                    ViolationType = violation.ViolationType,
                    ViolationTypeDisplay = GetViolationTypeDisplay(violation.ViolationType),
                    Description = violation.Description,
                    DamagePercentage = violation.DamagePercentage,
                    PenaltyPercentage = violation.PenaltyPercentage,
                    PenaltyAmount = violation.PenaltyAmount,
                    DepositAmount = depositAmount,
                    RefundAmount = refundAmount,
                    Status = violation.Status,
                    StatusDisplay = GetViolationStatusDisplay(violation.Status),
                    CustomerNotes = violation.CustomerNotes,
                    CustomerResponseAt = violation.CustomerResponseAt,
                    ProviderResponseToCustomer = violation.ProviderResponseToCustomer,
                    ProviderResponseAt = violation.ProviderResponseAt,
                    CreatedAt = violation.CreatedAt,
                    UpdatedAt = violation.UpdatedAt,
                    OrderItem = _mapper.Map<OrderItemDetailsDto>(violation.OrderItem),
                    Images = _mapper.Map<List<RentalViolationImageDto>>(violation.Images)
                };

                detailDtos.Add(detailDto);
            }

            return detailDtos;
        }

        public async Task<IEnumerable<RentalViolationDto>> GetCustomerViolationsAsync(Guid customerId)
        {
            var violations = await _violationRepo.GetViolationsByCustomerIdAsync(customerId);
            return _mapper.Map<IEnumerable<RentalViolationDto>>(violations);
        }

        public async Task<IEnumerable<RentalViolationDto>> GetProviderViolationsAsync(Guid providerId)
        {
            var violations = await _violationRepo.GetViolationsByProviderIdAsync(providerId);
            return _mapper.Map<IEnumerable<RentalViolationDto>>(violations);
        }

        public async Task<bool> UpdateViolationByProviderAsync(Guid violationId, UpdateViolationDto dto, Guid providerId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify provider owns this violation
            if (violation.OrderItem.Order.ProviderId != providerId)
                throw new UnauthorizedAccessException("Bạn không có quyền sửa vi phạm này");

            // Only allow update if customer rejected
            if (violation.Status != ViolationStatus.CUSTOMER_REJECTED)
                throw new InvalidOperationException("Chỉ có thể sửa khi khách hàng từ chối");

            // Update fields
            if (!string.IsNullOrEmpty(dto.Description))
                violation.Description = dto.Description;

            if (dto.PenaltyPercentage.HasValue)
                violation.PenaltyPercentage = dto.PenaltyPercentage.Value;

            if (dto.PenaltyAmount.HasValue)
                violation.PenaltyAmount = dto.PenaltyAmount.Value;

            // Reset customer response
            violation.CustomerNotes = null;
            violation.CustomerResponseAt = null;
            violation.Status = ViolationStatus.PENDING; // Back to pending

            return await _violationRepo.UpdateViolationAsync(violation);
        }

        public async Task<bool> EditViolationByProviderAsync(Guid violationId, UpdateViolationDto dto, Guid providerId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify provider owns this violation
            if (violation.OrderItem.Order.ProviderId != providerId)
                throw new UnauthorizedAccessException("Bạn không có quyền sửa vi phạm này");

            // Allow edit regardless of status (for edit mode)
            // Update all fields that are provided
            if (dto.ViolationType.HasValue)
                violation.ViolationType = dto.ViolationType.Value;

            if (!string.IsNullOrEmpty(dto.Description))
                violation.Description = dto.Description;

            if (dto.DamagePercentage.HasValue)
                violation.DamagePercentage = dto.DamagePercentage.Value;

            if (dto.PenaltyPercentage.HasValue)
                violation.PenaltyPercentage = dto.PenaltyPercentage.Value;

            if (dto.PenaltyAmount.HasValue)
                violation.PenaltyAmount = dto.PenaltyAmount.Value;

            // Keep existing status (don't reset to PENDING like UpdateViolationByProviderAsync)
            violation.UpdatedAt = DateTimeHelper.GetVietnamTime();

            return await _violationRepo.UpdateViolationAsync(violation);
        }

        public async Task<bool> CustomerRespondToViolationAsync(Guid violationId, CustomerViolationResponseDto dto, Guid customerId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify customer owns this violation
            if (violation.OrderItem.Order.CustomerId != customerId)
                throw new UnauthorizedAccessException("Bạn không có quyền phản hồi vi phạm này");

            // Allow response if status is PENDING or CUSTOMER_REJECTED (customer can change their decision)
            if (violation.Status != ViolationStatus.PENDING && violation.Status != ViolationStatus.CUSTOMER_REJECTED)
                throw new InvalidOperationException("Vi phạm này đã được xử lý");

            if (dto.IsAccepted)
            {
                // Customer accepts
                violation.Status = ViolationStatus.CUSTOMER_ACCEPTED;
                violation.CustomerResponseAt = DateTimeHelper.GetVietnamTime();

                // Update violation first
                var updateResult = await _violationRepo.UpdateViolationAsync(violation);

                // Check if all violations in this order are resolved and update order status if needed
                // This will also create/update deposit refund ONLY when ALL violations are resolved
                await CheckAndUpdateOrderStatusIfAllViolationsResolvedAsync(violation.OrderItem.Order.Id);

                return updateResult;
            }
            else
            {
                // Customer rejects - does not automatically escalate to admin
                violation.Status = ViolationStatus.CUSTOMER_REJECTED;
                violation.CustomerNotes = dto.CustomerNotes;
                violation.CustomerResponseAt = DateTimeHelper.GetVietnamTime();

                // Upload customer's evidence if any
                if (dto.EvidenceFiles != null && dto.EvidenceFiles.Any())
                {
                    foreach (var file in dto.EvidenceFiles)
                    {
                        var uploadResult = await _cloudinaryService.UploadMediaFileAsync(
                            file,
                            customerId,
                            "ShareIt",
                            "violations"
                        );

                        string fileType = file.ContentType.StartsWith("image/") ? "image" : "video";

                        var image = new RentalViolationImage
                        {
                            ImageId = Guid.NewGuid(),
                            ViolationId = violation.ViolationId,
                            ImageUrl = uploadResult.ImageUrl,
                            UploadedBy = EvidenceUploadedBy.CUSTOMER,
                            FileType = fileType,
                            UploadedAt = DateTimeHelper.GetVietnamTime()
                        };

                        await _violationRepo.AddEvidenceImageAsync(image);
                    }
                }

                // Notify provider that customer rejected
                await _notificationService.SendNotification(
                    violation.OrderItem.Order.ProviderId,
                    $"❌ Customer has rejected the violation claim. You can adjust the claim or escalate to admin for review.",
                    NotificationType.order,
                    violation.OrderItem.OrderId
                );
            }

            return await _violationRepo.UpdateViolationAsync(violation);
        }

        public async Task ProcessViolationFinancialAsync(Guid violationId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null || violation.Status != ViolationStatus.CUSTOMER_ACCEPTED)
                return;

            // TODO: Implement financial processing
            // 1. Calculate deposit amount and refund amount
            // 2. Create transactions:
            //    - Deduct PenaltyAmount from customer's deposit
            //    - Transfer PenaltyAmount to provider
            //    - Refund remaining deposit to customer
            // 3. Update violation status to RESOLVED

            violation.Status = ViolationStatus.RESOLVED;
            await _violationRepo.UpdateViolationAsync(violation);

            // Check if all violations in this order are resolved and update order status if needed
            await CheckAndUpdateOrderStatusIfAllViolationsResolvedAsync(violation.OrderItem.Order.Id);
        }

        public async Task<bool> CanUserAccessViolationAsync(Guid violationId, Guid userId, UserRole role)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Admin can access all
            if (role == UserRole.admin || role == UserRole.staff)
                return true;

            // Provider can access if they created it
            if (role == UserRole.provider && violation.OrderItem.Order.ProviderId == userId)
                return true;

            // Customer can access if it's their order
            if (role == UserRole.customer && violation.OrderItem.Order.CustomerId == userId)
                return true;

            return false;
        }

        /// <summary>
        /// Creates or updates deposit refund request ONLY when ALL violations are resolved
        /// This ensures refund is created with complete penalty information
        /// Similar to admin's Submit final decision logic
        /// </summary>
        private async Task CreateOrUpdateDepositRefundAfterAcceptanceAsync(Guid orderId)
        {
            // Get order with items
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.TotalDeposit <= 0)
            {
                return;
            }

            // Calculate total penalties from all accepted violations
            var totalPenalties = await _context.RentalViolations
                .Where(v => order.Items.Select(i => i.Id).Contains(v.OrderItemId) &&
                           (v.Status == ViolationStatus.CUSTOMER_ACCEPTED ||
                            v.Status == ViolationStatus.RESOLVED ||
                            v.Status == ViolationStatus.RESOLVED_BY_ADMIN))
                .SumAsync(v => v.PenaltyAmount);

            // Check if deposit refund already exists
            var existingRefund = await _context.DepositRefunds
                .FirstOrDefaultAsync(dr => dr.OrderId == orderId);

            if (existingRefund != null)
            {
                // Update existing refund with new penalty amount (when all violations are resolved)
                existingRefund.TotalPenaltyAmount = totalPenalties;
                existingRefund.RefundAmount = Math.Max(0, order.TotalDeposit - totalPenalties);
                existingRefund.Notes = $"Updated after all violations resolved. Total penalties: {totalPenalties:N0} ₫";
                _context.DepositRefunds.Update(existingRefund);
            }
            else
            {
                // Create new deposit refund (only when all violations are resolved)
                var depositRefund = new DepositRefund
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    CustomerId = order.CustomerId,
                    OriginalDepositAmount = order.TotalDeposit,
                    TotalPenaltyAmount = totalPenalties,
                    RefundAmount = Math.Max(0, order.TotalDeposit - totalPenalties),
                    Status = TransactionStatus.initiated,
                    Notes = totalPenalties > 0
                        ? $"Deposit refund after all violations resolved. Total penalties: {totalPenalties:N0} ₫"
                        : "Full deposit refund - no penalties",
                    CreatedAt = DateTimeHelper.GetVietnamTime()
                };

                await _context.DepositRefunds.AddAsync(depositRefund);
            }

            await _context.SaveChangesAsync();
        }

        public async Task CheckAndUpdateOrderStatusIfAllViolationsResolvedAsync(Guid orderId)
        {
            // Lấy order hiện tại
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null)
                return;

            // Lấy tất cả violations của order này
            var violations = await _violationRepo.GetViolationsByOrderIdAsync(orderId);

            // Nếu không có violation nào, không cần xử lý
            if (!violations.Any())
            {
                return;
            }

            // Kiểm tra xem tất cả violations đã được xử lý chưa
            // Violation được coi là đã xử lý khi status = CUSTOMER_ACCEPTED hoặc RESOLVED hoặc RESOLVED_BY_ADMIN
            // Violation chưa xử lý: PENDING, CUSTOMER_REJECTED
            var allViolationsResolved = violations.All(v =>
                v.Status == ViolationStatus.CUSTOMER_ACCEPTED ||
                v.Status == ViolationStatus.RESOLVED ||
                v.Status == ViolationStatus.RESOLVED_BY_ADMIN);

            // CHỈ tạo/update deposit refund khi TẤT CẢ violations đã được resolve
            // Điều này đảm bảo refund chỉ được tạo khi đã có đầy đủ thông tin về tất cả penalties
            if (allViolationsResolved)
            {
                // Create or update deposit refund request ONLY when ALL violations are resolved
                await CreateOrUpdateDepositRefundAfterAcceptanceAsync(orderId);

                // Nếu order status là returned_with_issue, chuyển sang returned
                if (order.Status == OrderStatus.returned_with_issue)
                {
                    var oldStatus = order.Status; // Store old status for UpdateProductCounts
                    order.Status = OrderStatus.returned;
                    order.UpdatedAt = DateTimeHelper.GetVietnamTime();

                    // Update product counts (RentCount/BuyCount) when order becomes returned
                    await UpdateProductCounts(order, oldStatus, OrderStatus.returned);

                    await _orderRepo.UpdateAsync(order);

                    // Gửi thông báo
                    await _notificationService.SendNotification(
                        order.CustomerId,
                        "✅ All violation issues have been resolved. Your order has been marked as returned.",
                        NotificationType.order,
                        order.Id
                    );

                    await _notificationService.SendNotification(
                        order.ProviderId,
                        $"✅ All violation issues for order #{order.Id} have been resolved. Order status updated to returned.",
                        NotificationType.order,
                        order.Id
                    );
                }
            }
        }

        public async Task<bool> ResolveOrderWithViolationsAsync(Guid orderId)
        {
            // Lấy order hiện tại
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null)
                return false;

            // Chỉ cho phép resolve order có status return_with_issue
            if (order.Status != OrderStatus.returned_with_issue)
                return false;

            // Lấy tất cả violations của order này
            var violations = await _violationRepo.GetViolationsByOrderIdAsync(orderId);

            // Kiểm tra xem tất cả violations đã được xử lý chưa
            // Violation được coi là đã xử lý khi status = CUSTOMER_ACCEPTED hoặc RESOLVED
            var allViolationsResolved = violations.All(v =>
                v.Status == ViolationStatus.CUSTOMER_ACCEPTED ||
                v.Status == ViolationStatus.RESOLVED);

            // Nếu chưa tất cả violations được xử lý, không cho phép resolve
            if (!allViolationsResolved || !violations.Any())
                return false;

            try
            {
                var oldStatus = order.Status; // Store old status for UpdateProductCounts
                order.Status = OrderStatus.returned;
                order.UpdatedAt = DateTimeHelper.GetVietnamTime();

                // Update product counts (RentCount/BuyCount) when order becomes returned
                await UpdateProductCounts(order, oldStatus, OrderStatus.returned);

                await _orderRepo.UpdateAsync(order);

                // Gửi thông báo
                await _notificationService.SendNotification(
                    order.CustomerId,
                    "✅ Your order with violation issues has been resolved and marked as returned.",
                    NotificationType.order,
                    order.Id
                );

                await _notificationService.SendNotification(
                    order.ProviderId,
                    $"✅ Order #{order.Id} with violation issues has been resolved and marked as returned. You can now receive payment.",
                    NotificationType.order,
                    order.Id
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates product rent count, buy count, and stock quantities based on order status
        /// - Rent count increases whenever order status becomes 'returned' (including returned_with_issue -> returned)
        /// - Buy count increases only when purchase order first reaches 'approved'
        /// </summary>
        /// <param name="order">The order containing items to update counts for</param>
        /// <param name="oldStatus">The previous order status</param>
        /// <param name="newStatus">The new order status</param>
        private async Task UpdateProductCounts(Order order, OrderStatus oldStatus, OrderStatus newStatus)
        {
            foreach (var item in order.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null) continue;

                bool shouldUpdateRentCount = false;
                bool shouldUpdateBuyCount = false;

                // For rental transactions - increment rent count whenever status becomes 'returned'
                if (item.TransactionType == BusinessObject.Enums.TransactionType.rental)
                {
                    if (newStatus == OrderStatus.returned)
                    {
                        shouldUpdateRentCount = true;
                    }
                }
                // For purchase transactions - increment buy count only when first time reaching 'approved'
                else if (item.TransactionType == BusinessObject.Enums.TransactionType.purchase)
                {
                    if (newStatus == OrderStatus.approved && oldStatus != OrderStatus.approved)
                    {
                        shouldUpdateBuyCount = true;
                    }
                }

                // Update counts if needed
                if (shouldUpdateRentCount)
                {
                    product.RentCount += item.Quantity;
                    await _productRepository.UpdateAsync(product);
                }
                else if (shouldUpdateBuyCount)
                {
                    product.BuyCount += item.Quantity;
                    await _productRepository.UpdateAsync(product);
                }
            }
        }

        #region Helper Methods

        private string GetViolationTypeDisplay(ViolationType type)
        {
            return type switch
            {
                ViolationType.DAMAGED => "Sản phẩm bị hư hỏng",
                ViolationType.LATE_RETURN => "Trả trễ hạn",
                ViolationType.NOT_RETURNED => "Không trả lại",
                _ => type.ToString()
            };
        }

        private string GetViolationStatusDisplay(ViolationStatus status)
        {
            return status switch
            {
                ViolationStatus.PENDING => "Chờ phản hồi",
                ViolationStatus.CUSTOMER_ACCEPTED => "Khách hàng đã đồng ý",
                ViolationStatus.CUSTOMER_REJECTED => "Khách hàng từ chối",
                ViolationStatus.PENDING_ADMIN_REVIEW => "Chờ Admin xem xét",
                ViolationStatus.RESOLVED_BY_ADMIN => "Admin đã giải quyết",
                ViolationStatus.RESOLVED => "Đã giải quyết",
                _ => status.ToString()
            };
        }

        #endregion

        public async Task<bool> EscalateViolationToAdminAsync(Guid violationId, Guid userId, UserRole userRole, string? escalationReason = null)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify user has permission to escalate
            bool canEscalate = false;
            if (userRole == UserRole.customer && violation.OrderItem.Order.CustomerId == userId)
            {
                canEscalate = true;
            }
            else if (userRole == UserRole.provider && violation.OrderItem.Order.ProviderId == userId)
            {
                canEscalate = true;
            }

            if (!canEscalate)
            {
                throw new UnauthorizedAccessException("You do not have permission to escalate this violation");
            }

            // Only allow escalation if status is PENDING or CUSTOMER_REJECTED
            if (violation.Status != ViolationStatus.PENDING && violation.Status != ViolationStatus.CUSTOMER_REJECTED)
            {
                throw new InvalidOperationException("This violation cannot be escalated at this time");
            }

            // Update status to PENDING_ADMIN_REVIEW
            violation.Status = ViolationStatus.PENDING_ADMIN_REVIEW;
            violation.UpdatedAt = DateTimeHelper.GetVietnamTime();

            // Store escalation reason in separate fields based on who escalated
            if (!string.IsNullOrEmpty(escalationReason))
            {
                if (userRole == UserRole.customer)
                {
                    violation.CustomerEscalationReason = escalationReason;
                }
                else if (userRole == UserRole.provider)
                {
                    violation.ProviderEscalationReason = escalationReason;
                }
            }

            var updateResult = await _violationRepo.UpdateViolationAsync(violation);

            if (updateResult)
            {
                // Send notification to all admins
                var adminUsers = await _violationRepo.GetAdminUsersAsync();
                foreach (var admin in adminUsers)
                {
                    await _notificationService.SendNotification(
                        admin.Id,
                        $"⚖️ New dispute case requires review. A violation has been escalated to admin by {userRole}.",
                        NotificationType.order,
                        violation.OrderItem.OrderId
                    );
                }

                // Notify the other party
                if (userRole == UserRole.customer)
                {
                    await _notificationService.SendNotification(
                        violation.OrderItem.Order.ProviderId,
                        $"⚖️ Customer has escalated the violation dispute to admin for review.",
                        NotificationType.order,
                        violation.OrderItem.OrderId
                    );
                }
                else if (userRole == UserRole.provider)
                {
                    await _notificationService.SendNotification(
                        violation.OrderItem.Order.CustomerId,
                        $"⚖️ Provider has escalated the violation dispute to admin for review.",
                        NotificationType.order,
                        violation.OrderItem.OrderId
                    );
                }
            }

            return updateResult;
        }

        public async Task<bool> ProviderRespondToCustomerAsync(Guid violationId, string response, Guid providerId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify provider owns this violation
            if (violation.OrderItem.Order.ProviderId != providerId)
            {
                throw new UnauthorizedAccessException("You do not have permission to respond to this violation");
            }

            // Only allow response if customer has rejected (has CustomerNotes)
            if (string.IsNullOrWhiteSpace(violation.CustomerNotes))
            {
                throw new InvalidOperationException("Cannot respond - customer has not provided any rejection notes");
            }

            // Update provider response
            violation.ProviderResponseToCustomer = response;
            violation.ProviderResponseAt = DateTimeHelper.GetVietnamTime();
            violation.UpdatedAt = DateTimeHelper.GetVietnamTime();

            var updateResult = await _violationRepo.UpdateViolationAsync(violation);

            if (updateResult)
            {
                // Notify customer about provider's response
                await _notificationService.SendNotification(
                    violation.OrderItem.Order.CustomerId,
                    $"📝 Provider has responded to your rejection notes for the violation report.",
                    NotificationType.order,
                    violation.OrderItem.OrderId
                );
            }

            return updateResult;
        }
    }
}
