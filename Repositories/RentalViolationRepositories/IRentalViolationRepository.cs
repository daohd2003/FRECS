using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.RentalViolationRepositories
{
    public interface IRentalViolationRepository : IRepository<RentalViolation>
    {
        /// <summary>
        /// Lấy vi phạm theo ID kèm OrderItem và Images
        /// </summary>
        Task<RentalViolation?> GetViolationWithDetailsAsync(Guid violationId);

        /// <summary>
        /// Lấy tất cả vi phạm của 1 đơn hàng
        /// </summary>
        Task<IEnumerable<RentalViolation>> GetViolationsByOrderIdAsync(Guid orderId);

        /// <summary>
        /// Lấy tất cả vi phạm của 1 OrderItem cụ thể
        /// </summary>
        Task<IEnumerable<RentalViolation>> GetViolationsByOrderItemIdAsync(Guid orderItemId);

        /// <summary>
        /// Lấy vi phạm theo Provider (lấy tất cả vi phạm mà Provider đã tạo)
        /// </summary>
        Task<IEnumerable<RentalViolation>> GetViolationsByProviderIdAsync(Guid providerId);

        /// <summary>
        /// Lấy vi phạm theo Customer (lấy tất cả vi phạm liên quan đến Customer)
        /// </summary>
        Task<IEnumerable<RentalViolation>> GetViolationsByCustomerIdAsync(Guid customerId);

        /// <summary>
        /// Cập nhật vi phạm (khi Provider điều chỉnh hoặc Customer phản hồi)
        /// </summary>
        Task<bool> UpdateViolationAsync(RentalViolation violation);

        /// <summary>
        /// Thêm ảnh bằng chứng vào vi phạm
        /// </summary>
        Task AddEvidenceImageAsync(RentalViolationImage image);

        /// <summary>
        /// Lấy ảnh bằng chứng theo vi phạm
        /// </summary>
        Task<IEnumerable<RentalViolationImage>> GetEvidenceImagesAsync(Guid violationId);
    }
}