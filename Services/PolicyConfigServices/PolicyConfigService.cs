using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PolicyConfigDto;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.PolicyConfigRepositories;

namespace Services.PolicyConfigServices
{
    public class PolicyConfigService : IPolicyConfigService
    {
        private readonly IPolicyConfigRepository _policyRepository;

        public PolicyConfigService(IPolicyConfigRepository policyRepository)
        {
            _policyRepository = policyRepository;
        }

        public async Task<ApiResponse<IEnumerable<PolicyConfigDto>>> GetAllPoliciesAsync()
        {
            try
            {
                var policies = await _policyRepository.GetAllPoliciesAsync();
                var policyDtos = policies.Select(MapToDto);
                return new ApiResponse<IEnumerable<PolicyConfigDto>>("Policies retrieved successfully", policyDtos);
            }
            catch (Exception ex)
            {
                return new ApiResponse<IEnumerable<PolicyConfigDto>>($"Error retrieving policies: {ex.Message}", Enumerable.Empty<PolicyConfigDto>());
            }
        }

        public async Task<ApiResponse<IEnumerable<PolicyConfigDto>>> GetActivePoliciesAsync()
        {
            try
            {
                var policies = await _policyRepository.GetActivePoliciesAsync();
                var policyDtos = policies.Select(MapToDto);
                return new ApiResponse<IEnumerable<PolicyConfigDto>>("Active policies retrieved successfully", policyDtos);
            }
            catch (Exception ex)
            {
                return new ApiResponse<IEnumerable<PolicyConfigDto>>($"Error retrieving active policies: {ex.Message}", Enumerable.Empty<PolicyConfigDto>());
            }
        }

        public async Task<ApiResponse<PolicyConfigDto>> GetPolicyByIdAsync(Guid id)
        {
            try
            {
                var policy = await _policyRepository.GetPolicyByIdAsync(id);
                if (policy == null)
                {
                    return new ApiResponse<PolicyConfigDto>("Policy not found", null!);
                }

                return new ApiResponse<PolicyConfigDto>("Policy retrieved successfully", MapToDto(policy));
            }
            catch (Exception ex)
            {
                return new ApiResponse<PolicyConfigDto>($"Error retrieving policy: {ex.Message}", null!);
            }
        }

        public async Task<ApiResponse<PolicyConfigDto>> GetActivePolicyByNameAsync(string policyName)
        {
            try
            {
                var policy = await _policyRepository.GetActivePolicyByNameAsync(policyName);
                if (policy == null)
                {
                    return new ApiResponse<PolicyConfigDto>("Policy not found or inactive", null!);
                }

                return new ApiResponse<PolicyConfigDto>("Policy retrieved successfully", MapToDto(policy));
            }
            catch (Exception ex)
            {
                return new ApiResponse<PolicyConfigDto>($"Error retrieving policy: {ex.Message}", null!);
            }
        }

        public async Task<ApiResponse<PolicyConfigDto>> CreatePolicyAsync(CreatePolicyConfigDto dto, Guid adminId)
        {
            try
            {
                // Check if policy name already exists
                if (await _policyRepository.PolicyNameExistsAsync(dto.PolicyName))
                {
                    return new ApiResponse<PolicyConfigDto>("A policy with this name already exists", null!);
                }

                var policy = new PolicyConfig
                {
                    Id = Guid.NewGuid(),
                    PolicyName = dto.PolicyName,
                    Content = dto.Content,
                    IsActive = dto.IsActive,
                    UpdatedAt = DateTimeHelper.GetVietnamTime(),
                    UpdatedByAdminId = adminId
                };

                var createdPolicy = await _policyRepository.CreatePolicyAsync(policy);
                var result = await _policyRepository.GetPolicyByIdAsync(createdPolicy.Id);
                
                return new ApiResponse<PolicyConfigDto>("Policy created successfully", MapToDto(result!));
            }
            catch (Exception ex)
            {
                return new ApiResponse<PolicyConfigDto>($"Error creating policy: {ex.Message}", null!);
            }
        }

        public async Task<ApiResponse<PolicyConfigDto>> UpdatePolicyAsync(Guid id, UpdatePolicyConfigDto dto, Guid adminId)
        {
            try
            {
                var policy = await _policyRepository.GetPolicyByIdAsync(id);
                if (policy == null)
                {
                    return new ApiResponse<PolicyConfigDto>("Policy not found", null!);
                }

                // Check if new policy name conflicts with existing policies
                if (policy.PolicyName != dto.PolicyName && await _policyRepository.PolicyNameExistsAsync(dto.PolicyName, id))
                {
                    return new ApiResponse<PolicyConfigDto>("A policy with this name already exists", null!);
                }

                policy.PolicyName = dto.PolicyName;
                policy.Content = dto.Content;
                policy.IsActive = dto.IsActive;
                policy.UpdatedAt = DateTimeHelper.GetVietnamTime();
                policy.UpdatedByAdminId = adminId;

                await _policyRepository.UpdatePolicyAsync(policy);
                var updatedPolicy = await _policyRepository.GetPolicyByIdAsync(id);
                
                return new ApiResponse<PolicyConfigDto>("Policy updated successfully", MapToDto(updatedPolicy!));
            }
            catch (Exception ex)
            {
                return new ApiResponse<PolicyConfigDto>($"Error updating policy: {ex.Message}", null!);
            }
        }

        public async Task<ApiResponse<bool>> DeletePolicyAsync(Guid id)
        {
            try
            {
                var policy = await _policyRepository.GetPolicyByIdAsync(id);
                if (policy == null)
                {
                    return new ApiResponse<bool>("Policy not found", false);
                }

                var result = await _policyRepository.DeletePolicyAsync(id);
                return new ApiResponse<bool>("Policy deleted successfully", result);
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>($"Error deleting policy: {ex.Message}", false);
            }
        }

        private static PolicyConfigDto MapToDto(PolicyConfig policy)
        {
            return new PolicyConfigDto
            {
                Id = policy.Id,
                PolicyName = policy.PolicyName,
                Content = policy.Content,
                IsActive = policy.IsActive,
                UpdatedAt = policy.UpdatedAt,
                UpdatedByAdminId = policy.UpdatedByAdminId,
                UpdatedByAdminName = policy.UpdatedByAdmin?.Profile?.FullName ?? "Unknown"
            };
        }
    }
}
