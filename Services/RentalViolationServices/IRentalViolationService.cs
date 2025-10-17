using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.RentalViolationServices
{
    public interface IRentalViolationService
    {
        /// <summary>
        /// Tạo nhiều vi phạm cùng lúc (Provider báo cáo)
        /// </summary>
        Task<List<Guid>> CreateMultipleViolationsAsync(CreateMultipleViolationsRequestDto dto, Guid providerId);

        /// <summary>
        /// Lấy chi tiết vi phạm
        /// </summary>
        Task<RentalViolationDetailDto?> GetViolationDetailAsync(Guid violationId);

        /// <summary>
        /// Lấy tất cả vi phạm của 1 đơn hàng
        /// </summary>
        Task<IEnumerable<RentalViolationDto>> GetViolationsByOrderIdAsync(Guid orderId);

        /// <summary>
        /// Lấy tất cả vi phạm của Customer
        /// </summary>
        Task<IEnumerable<RentalViolationDto>> GetCustomerViolationsAsync(Guid customerId);

        /// <summary>
        /// Lấy tất cả vi phạm của Provider
        /// </summary>
        Task<IEnumerable<RentalViolationDto>> GetProviderViolationsAsync(Guid providerId);

        /// <summary>
        /// Provider điều chỉnh lại yêu cầu (sau khi Customer từ chối)
        /// </summary>
        Task<bool> UpdateViolationByProviderAsync(Guid violationId, UpdateViolationDto dto, Guid providerId);

        /// <summary>
        /// Customer phản hồi vi phạm (đồng ý hoặc từ chối)
        /// </summary>
        Task<bool> CustomerRespondToViolationAsync(Guid violationId, CustomerViolationResponseDto dto, Guid customerId);

        /// <summary>
        /// Xử lý tài chính khi vi phạm được giải quyết (Status = RESOLVED)
        /// </summary>
        Task ProcessViolationFinancialAsync(Guid violationId);

        /// <summary>
        /// Kiểm tra xem user có quyền xem vi phạm này không
        /// </summary>
        Task<bool> CanUserAccessViolationAsync(Guid violationId, Guid userId, UserRole role);
    }
}