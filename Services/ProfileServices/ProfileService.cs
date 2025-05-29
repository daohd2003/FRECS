using BusinessObject.Models;
using Repositories.ProfileRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.ProfileServices
{
    public class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepository;

        public ProfileService(IProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        public async Task<Profile?> GetByUserIdAsync(Guid userId)
        {
            return await _profileRepository.GetByUserIdAsync(userId);
        }

        public async Task AddAsync(Profile profile)
        {
            await _profileRepository.AddAsync(profile);
        }

        public async Task UpdateAsync(Profile profile)
        {
            await _profileRepository.UpdateAsync(profile);
        }
    }
}
