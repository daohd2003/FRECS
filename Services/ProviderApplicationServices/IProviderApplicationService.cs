using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;

namespace Services.ProviderApplicationServices
{
    public interface IProviderApplicationService
    {
        Task<ProviderApplication> ApplyAsync(Guid userId, ProviderApplicationCreateDto dto);
        Task<bool> ReviewAsync(Guid adminId, ProviderApplicationReviewDto dto);
        Task<ProviderApplication?> GetMyPendingAsync(Guid userId);
        Task<IEnumerable<ProviderApplication>> GetByStatusAsync(ProviderApplicationStatus status);
    }
}


