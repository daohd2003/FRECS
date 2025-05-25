using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Logout
{
    public interface ILoggedOutTokenRepository : IRepository<BlacklistedToken>
    {
        Task AddAsync(string token, DateTime expirationDate);
        Task<bool> IsTokenLoggedOutAsync(string token);
    }
}
