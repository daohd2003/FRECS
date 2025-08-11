using BusinessObject.DTOs.Login;
using BusinessObject.DTOs.ReportDto;
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
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetUserByEmailAsync(string email);
        Task AddAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(Guid id);
        Task<User> GetOrCreateUserAsync(GooglePayload payload);
        Task<User> GetOrCreateUserAsync(FacebookPayload payload);
        Task<IEnumerable<AdminViewModel>> GetAllAdminsAsync();
    }
}
