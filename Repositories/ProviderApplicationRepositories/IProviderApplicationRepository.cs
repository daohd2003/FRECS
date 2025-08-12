using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.ProviderApplicationRepositories
{
    public interface IProviderApplicationRepository : IRepository<ProviderApplication>
    {
        Task<ProviderApplication?> GetPendingByUserIdAsync(Guid userId);
        Task<IEnumerable<ProviderApplication>> GetByStatusAsync(ProviderApplicationStatus status);
    }
}


