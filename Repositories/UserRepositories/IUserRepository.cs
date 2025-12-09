using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.UserRepositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> GetOrCreateUserAsync(GooglePayload payload);
        Task<User> GetOrCreateUserAsync(FacebookPayload payload);
        Task<User?> GetByRefreshTokenAsync(string refreshToken);
        Task<IEnumerable<User>> GetAllWithOrdersAsync();
        Task<User?> GetUserWithOrdersAsync(Guid userId);
        Task<IEnumerable<(User User, int OrderCount)>> GetAllUsersWithOrderCountAsync();
        Task<UserOrderStatsDto?> GetUserOrderStatsOptimizedAsync(Guid userId);
    }
}
