using BusinessObject.DTOs.IssueResolutionDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories.CompensationDisputeRepositories
{
    public interface ICompensationDisputeRepository
    {
        /// <summary>
        /// Lấy danh sách các tranh chấp đang chờ Admin xem xét
        /// </summary>
        Task<List<DisputeCaseListDto>> GetPendingDisputeCasesAsync();

        /// <summary>
        /// Lấy chi tiết một vụ tranh chấp
        /// </summary>
        Task<DisputeCaseDetailDto?> GetDisputeCaseDetailAsync(Guid violationId);

        /// <summary>
        /// Tạo quyết định mới từ Admin
        /// </summary>
        Task<IssueResolution> CreateResolutionAsync(IssueResolution resolution);

        /// <summary>
        /// Cập nhật trạng thái của violation
        /// </summary>
        Task<bool> UpdateViolationStatusAsync(Guid violationId, BusinessObject.Enums.ViolationStatus status);

        /// <summary>
        /// Kiểm tra xem violation có tồn tại không
        /// </summary>
        Task<bool> ViolationExistsAsync(Guid violationId);

        /// <summary>
        /// Lấy thông tin violation
        /// </summary>
        Task<RentalViolation?> GetViolationAsync(Guid violationId);

        /// <summary>
        /// Cập nhật violation
        /// </summary>
        Task<bool> UpdateViolationAsync(RentalViolation violation);
    }
}

