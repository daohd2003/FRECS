using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.ProviderApplicationRepositories;
using Repositories.UserRepositories;

namespace Services.ProviderApplicationServices
{
    public class ProviderApplicationService : IProviderApplicationService
    {
        private readonly IProviderApplicationRepository _applicationRepository;
        private readonly IUserRepository _userRepository;

        public ProviderApplicationService(IProviderApplicationRepository applicationRepository, IUserRepository userRepository)
        {
            _applicationRepository = applicationRepository;
            _userRepository = userRepository;
        }

        public async Task<ProviderApplication> ApplyAsync(Guid userId, ProviderApplicationCreateDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId) ?? throw new InvalidOperationException("User not found");

            if (user.Role == UserRole.provider)
            {
                throw new InvalidOperationException("User is already a provider");
            }

            var existingPending = await _applicationRepository.GetPendingByUserIdAsync(userId);
            if (existingPending != null)
            {
                return existingPending;
            }

            var app = new ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = dto.BusinessName,
                TaxId = dto.TaxId,
                ContactPhone = dto.ContactPhone,
                Notes = dto.Notes,
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTimeHelper.GetVietnamTime()
            };

            await _applicationRepository.AddAsync(app);
            return app;
        }

        public async Task<bool> ReviewAsync(Guid adminId, ProviderApplicationReviewDto dto)
        {
            var app = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
            if (app == null) return false;

            if (app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            app.Status = dto.NewStatus;
            app.ReviewedAt = DateTime.UtcNow;
            app.ReviewedByAdminId = adminId;
            app.ReviewComment = dto.ReviewComment;

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            if (dto.NewStatus == ProviderApplicationStatus.approved)
            {
                var user = await _userRepository.GetByIdAsync(app.UserId);
                if (user == null) return false;
                user.Role = UserRole.provider;
                await _userRepository.UpdateAsync(user);
            }

            return true;
        }

        public Task<ProviderApplication?> GetMyPendingAsync(Guid userId)
        {
            return _applicationRepository.GetPendingByUserIdAsync(userId);
        }

        public Task<IEnumerable<ProviderApplication>> GetByStatusAsync(ProviderApplicationStatus status)
        {
            return _applicationRepository.GetByStatusAsync(status);
        }
    }
}


