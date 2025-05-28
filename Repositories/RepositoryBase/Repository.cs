using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.RepositoryBase
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ShareItDbContext _context;

        public Repository(ShareItDbContext context)
        {
            _context = context;
        }

        public virtual async Task AddAsync(T entity)
        {
            await _context.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Set<T>().FindAsync(id);

            if (entity == null) return false;

            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync();

            return true;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id) ?? null;
        }

        public virtual async Task<bool> UpdateAsync(T entity)
        {
            var keyNames = _context.Model
                .FindEntityType(typeof(T))?
                .FindPrimaryKey()?
                .Properties
                .Select(x => x.Name)
                .ToArray();

            if (keyNames == null || keyNames.Length == 0)
                throw new Exception("No primary key defined.");

            var keyValues = keyNames
                .Select(name => typeof(T).GetProperty(name)!.GetValue(entity))
                .ToArray();

            var exists = await _context.Set<T>().FindAsync(keyValues);
            if (exists == null) throw new Exception("Entity not found.");

            _context.Entry(exists).CurrentValues.SetValues(entity);
            var affectedRows = await _context.SaveChangesAsync();

            return affectedRows > 0;
        }
    }
}
