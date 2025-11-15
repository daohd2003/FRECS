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
        Task<IEnumerable<ProviderApplication>> GetAllApplicationsAsync(ProviderApplicationStatus? status);
        Task<bool> ApproveAsync(Guid staffId, Guid applicationId);
        Task<bool> RejectAsync(Guid staffId, Guid applicationId, string rejectionReason);
        Task<Dictionary<string, string>> GetApplicationImagesWithSignedUrlsAsync(Guid applicationId, Guid requesterId);
    }
}


