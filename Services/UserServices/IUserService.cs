using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.ReportDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.UserServices
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<IEnumerable<User>> GetAllWithOrdersAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetUserByEmailAsync(string email);
        Task AddAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(Guid id);
        Task<User> GetOrCreateUserAsync(GooglePayload payload);
        Task<User> GetOrCreateUserAsync(FacebookPayload payload);
        Task<IEnumerable<AdminViewModel>> GetAllAdminsAsync();
        Task<bool> BlockUserAsync(Guid id);
        Task<bool> UnblockUserAsync(Guid id);
        Task<IEnumerable<User>> GetCustomersAndProvidersAsync();
        Task<UserOrderStatsDto?> GetUserOrderStatsAsync(Guid userId);
        Task<IEnumerable<UserWithOrderStatsDto>> GetAllUsersWithOrderStatsAsync(bool staffOnly = false);
    }
}
