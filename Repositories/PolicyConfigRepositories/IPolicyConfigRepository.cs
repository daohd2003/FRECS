using BusinessObject.Models;

namespace Repositories.PolicyConfigRepositories
{
    public interface IPolicyConfigRepository
    {
        Task<IEnumerable<PolicyConfig>> GetAllPoliciesAsync();
        Task<IEnumerable<PolicyConfig>> GetActivePoliciesAsync();
        Task<PolicyConfig?> GetPolicyByIdAsync(Guid id);
        Task<PolicyConfig?> GetPolicyByNameAsync(string policyName);
        Task<PolicyConfig> CreatePolicyAsync(PolicyConfig policy);
        Task<PolicyConfig> UpdatePolicyAsync(PolicyConfig policy);
        Task<bool> DeletePolicyAsync(Guid id);
        Task<bool> PolicyNameExistsAsync(string policyName, Guid? excludeId = null);
    }
}
