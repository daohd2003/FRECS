using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.ProviderApplicationRepositories;
using Repositories.UserRepositories;
using Services.EmailServices;

namespace Services.ProviderApplicationServices
{
    public class ProviderApplicationService : IProviderApplicationService
    {
        private readonly IProviderApplicationRepository _applicationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;

        public ProviderApplicationService(
            IProviderApplicationRepository applicationRepository, 
            IUserRepository userRepository,
            IEmailService emailService)
        {
            _applicationRepository = applicationRepository;
            _userRepository = userRepository;
            _emailService = emailService;
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

        public async Task<IEnumerable<ProviderApplication>> GetAllApplicationsAsync(ProviderApplicationStatus? status)
        {
            if (status.HasValue)
            {
                return await _applicationRepository.GetByStatusAsync(status.Value);
            }
            return await _applicationRepository.GetAllWithUserDetailsAsync();
        }

        public async Task<bool> ApproveAsync(Guid staffId, Guid applicationId)
        {
            var app = await _applicationRepository.GetByIdAsync(applicationId);
            if (app == null || app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            // Update application status
            app.Status = ProviderApplicationStatus.approved;
            app.ReviewedAt = DateTimeHelper.GetVietnamTime();
            app.ReviewedByAdminId = staffId;
            app.ReviewComment = "Application approved";

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            // Update user role to provider
            var user = await _userRepository.GetByIdAsync(app.UserId);
            if (user == null) return false;
            
            user.Role = UserRole.provider;
            await _userRepository.UpdateAsync(user);

            // Send approval email
            try
            {
                await _emailService.SendProviderApplicationApprovedEmailAsync(user.Email, app.BusinessName);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Failed to send approval email: {ex.Message}");
            }

            return true;
        }

        public async Task<bool> RejectAsync(Guid staffId, Guid applicationId, string rejectionReason)
        {
            var app = await _applicationRepository.GetByIdAsync(applicationId);
            if (app == null || app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            // Update application status
            app.Status = ProviderApplicationStatus.rejected;
            app.ReviewedAt = DateTimeHelper.GetVietnamTime();
            app.ReviewedByAdminId = staffId;
            app.ReviewComment = rejectionReason;

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            // Get user for email
            var user = await _userRepository.GetByIdAsync(app.UserId);
            if (user == null) return false;

            // Send rejection email
            try
            {
                await _emailService.SendProviderApplicationRejectedEmailAsync(
                    user.Email, 
                    app.BusinessName, 
                    rejectionReason);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Failed to send rejection email: {ex.Message}");
            }

            return true;
        }
    }
}


