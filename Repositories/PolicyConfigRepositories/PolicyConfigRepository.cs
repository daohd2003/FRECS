using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Repositories.PolicyConfigRepositories
{
    public class PolicyConfigRepository : IPolicyConfigRepository
    {
        private readonly ShareItDbContext _context;

        public PolicyConfigRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PolicyConfig>> GetAllPoliciesAsync()
        {
            return await _context.PolicyConfigs
                .Include(p => p.UpdatedByAdmin)
                    .ThenInclude(u => u!.Profile)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<PolicyConfig>> GetActivePoliciesAsync()
        {
            return await _context.PolicyConfigs
                .Where(p => p.IsActive)
                .OrderBy(p => p.PolicyName)
                .ToListAsync();
        }

        public async Task<PolicyConfig?> GetPolicyByIdAsync(Guid id)
        {
            return await _context.PolicyConfigs
                .Include(p => p.UpdatedByAdmin)
                    .ThenInclude(u => u!.Profile)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PolicyConfig?> GetPolicyByNameAsync(string policyName)
        {
            return await _context.PolicyConfigs
                .FirstOrDefaultAsync(p => p.PolicyName == policyName);
        }

        public async Task<PolicyConfig?> GetActivePolicyByNameAsync(string policyName)
        {
            return await _context.PolicyConfigs
                .FirstOrDefaultAsync(p => p.PolicyName == policyName && p.IsActive);
        }

        public async Task<PolicyConfig> CreatePolicyAsync(PolicyConfig policy)
        {
            _context.PolicyConfigs.Add(policy);
            await _context.SaveChangesAsync();
            return policy;
        }

        public async Task<PolicyConfig> UpdatePolicyAsync(PolicyConfig policy)
        {
            _context.PolicyConfigs.Update(policy);
            await _context.SaveChangesAsync();
            return policy;
        }

        public async Task<bool> DeletePolicyAsync(Guid id)
        {
            var policy = await _context.PolicyConfigs.FindAsync(id);
            if (policy == null) return false;

            _context.PolicyConfigs.Remove(policy);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PolicyNameExistsAsync(string policyName, Guid? excludeId = null)
        {
            var query = _context.PolicyConfigs.Where(p => p.PolicyName == policyName);
            
            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
