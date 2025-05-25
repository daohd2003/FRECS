using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Logout
{
    public class LoggedOutTokenRepository : Repository<BlacklistedToken>, ILoggedOutTokenRepository
    {
        public LoggedOutTokenRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task AddAsync(string token, DateTime expirationDate)
        {
            var loggedOutToken = new BlacklistedToken { Token = token, ExpiredAt = expirationDate };

            await _context.BlacklistedTokens.AddAsync(loggedOutToken);

            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsTokenLoggedOutAsync(string token)
        {
            return await _context.BlacklistedTokens.AnyAsync(bt => bt.Token == token && bt.ExpiredAt > DateTime.UtcNow);
        }
    }
}
