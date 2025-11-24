using BusinessObject.DTOs.IssueResolutionDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.CompensationDisputeServices
{
    public interface ICompensationDisputeService
    {
        /// <summary>
        /// Lấy danh sách các tranh chấp đang chờ Admin xem xét
        /// </summary>
        Task<List<DisputeCaseListDto>> GetPendingDisputesAsync();

        /// <summary>
        /// Lấy chi tiết một vụ tranh chấp
        /// </summary>
        Task<DisputeCaseDetailDto?> GetDisputeDetailAsync(Guid violationId);

        /// <summary>
        /// Admin tạo quyết định cuối cùng cho tranh chấp
        /// </summary>
        Task<IssueResolutionResponseDto> CreateAdminResolutionAsync(CreateIssueResolutionDto dto, Guid adminId);
    }
}

