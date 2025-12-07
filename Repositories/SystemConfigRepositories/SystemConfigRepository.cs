using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Threading.Tasks;

namespace Repositories.SystemConfigRepositories
{
    public class SystemConfigRepository : Repository<SystemConfig>, ISystemConfigRepository
    {
        public SystemConfigRepository(ShareItDbContext context) : base(context)
        {
        }

        public async Task<SystemConfig?> GetByKeyAsync(string key)
        {
            return await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
        }

        public async Task<decimal> GetCommissionRateAsync(string key)
        {
            var config = await GetByKeyAsync(key);
            
            if (config == null)
            {
                throw new InvalidOperationException($"Commission rate configuration '{key}' not found in SystemConfigs table. Please configure it in Admin settings.");
            }
            
            if (!decimal.TryParse(config.Value, out decimal rate))
            {
                throw new InvalidOperationException($"Invalid commission rate value '{config.Value}' for key '{key}'. Expected a decimal number.");
            }
            
            // Validate rate is between 0 and 1 (0% to 100%)
            if (rate < 0 || rate > 1)
            {
                throw new InvalidOperationException($"Commission rate '{rate}' for key '{key}' is out of valid range (0.00 to 1.00).");
            }
            
            return rate;
        }

        public async Task UpdateConfigAsync(string key, string value, Guid? adminId)
        {
            var config = await GetByKeyAsync(key);
            
            if (config != null)
            {
                config.Value = value;
                config.UpdatedAt = DateTimeHelper.GetVietnamTime();
                config.UpdatedByAdminId = adminId;
                _context.SystemConfigs.Update(config);
            }
            else
            {
                config = new SystemConfig
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTimeHelper.GetVietnamTime(),
                    UpdatedByAdminId = adminId
                };
                _context.SystemConfigs.Add(config);
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ConfigExistsAsync(string key)
        {
            return await _context.SystemConfigs.AnyAsync(c => c.Key == key);
        }

        public async Task CreateConfigAsync(SystemConfig config)
        {
            _context.SystemConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
    }
}
