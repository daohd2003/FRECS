using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PolicyConfigDto;

namespace Services.PolicyConfigServices
{
    public interface IPolicyConfigService
    {
        Task<ApiResponse<IEnumerable<PolicyConfigDto>>> GetAllPoliciesAsync();
        Task<ApiResponse<IEnumerable<PolicyConfigDto>>> GetActivePoliciesAsync();
        Task<ApiResponse<PolicyConfigDto>> GetPolicyByIdAsync(Guid id);
        Task<ApiResponse<PolicyConfigDto>> CreatePolicyAsync(CreatePolicyConfigDto dto, Guid adminId);
        Task<ApiResponse<PolicyConfigDto>> UpdatePolicyAsync(Guid id, UpdatePolicyConfigDto dto, Guid adminId);
        Task<ApiResponse<bool>> DeletePolicyAsync(Guid id);
    }
}
