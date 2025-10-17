using AutoMapper;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.OrderRepositories;
using Repositories.RentalViolationRepositories;
using Services.CloudServices;
using Services.NotificationServices;

namespace Services.RentalViolationServices
{
    public class RentalViolationService : IRentalViolationService
    {
        private readonly IRentalViolationRepository _violationRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public RentalViolationService(
            IRentalViolationRepository violationRepo,
            IOrderRepository orderRepo,
            ICloudinaryService cloudinaryService,
            INotificationService notificationService,
            IMapper mapper)
        {
            _violationRepo = violationRepo;
            _orderRepo = orderRepo;
            _cloudinaryService = cloudinaryService;
            _notificationService = notificationService;
            _mapper = mapper;
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
                        throw new Exception($"This item has already been reported. Only one violation report per item is allowed.");
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
            return _mapper.Map<IEnumerable<RentalViolationDto>>(violations);
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

        public async Task<bool> CustomerRespondToViolationAsync(Guid violationId, CustomerViolationResponseDto dto, Guid customerId)
        {
            var violation = await _violationRepo.GetViolationWithDetailsAsync(violationId);
            if (violation == null)
                return false;

            // Verify customer owns this violation
            if (violation.OrderItem.Order.CustomerId != customerId)
                throw new UnauthorizedAccessException("Bạn không có quyền phản hồi vi phạm này");

            // Only allow response if status is PENDING
            if (violation.Status != ViolationStatus.PENDING)
                throw new InvalidOperationException("Vi phạm này đã được xử lý");

            if (dto.IsAccepted)
            {
                // Customer accepts
                violation.Status = ViolationStatus.CUSTOMER_ACCEPTED;
                violation.CustomerResponseAt = DateTime.UtcNow;

                // TODO: Process financial transaction here
                // await ProcessViolationFinancialAsync(violationId);
            }
            else
            {
                // Customer rejects
                violation.Status = ViolationStatus.CUSTOMER_REJECTED;
                violation.CustomerNotes = dto.CustomerNotes;
                violation.CustomerResponseAt = DateTime.UtcNow;

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
                ViolationStatus.RESOLVED => "Đã giải quyết",
                _ => status.ToString()
            };
        }

        #endregion
    }
}
