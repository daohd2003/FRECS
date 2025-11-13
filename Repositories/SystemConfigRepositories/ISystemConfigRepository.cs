using BusinessObject.Models;
using System;
using System.Threading.Tasks;

namespace Repositories.SystemConfigRepositories
{
    public interface ISystemConfigRepository
    {
        Task<SystemConfig?> GetByKeyAsync(string key);
        Task<decimal> GetCommissionRateAsync(string key);
        Task UpdateConfigAsync(string key, string value, Guid? adminId);
        Task<bool> ConfigExistsAsync(string key);
        Task CreateConfigAsync(SystemConfig config);
    }
}
