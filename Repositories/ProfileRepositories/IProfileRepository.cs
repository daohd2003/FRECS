using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ProfileRepositories
{
    public interface IProfileRepository
    {
        Task<Profile?> GetByUserIdAsync(Guid userId);
        Task AddAsync(Profile profile);
        Task UpdateAsync(Profile profile);
    }
}
