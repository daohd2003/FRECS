using AutoMapper;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;

namespace Repositories.CategoryRepositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        private readonly IMapper _mapper;

        public CategoryRepository(ShareItDbContext context, IMapper mapper) : base(context)
        {
            _mapper = mapper;
        }

        public async Task<List<Category>> GetAllCategoryAsync()
        {
            return await _context.Categories
                                 .Include(c => c.Products)
                                 .OrderBy(c => c.Name)
                                 .ToListAsync();
        }

        public async Task<Category?> GetByNameAsync(string name)
        {
            return await _context.Categories
                                 .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }

        public async Task AddCategoryAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(Guid id)
        {
            var entity = await _context.Categories.FindAsync(id);
            if (entity != null)
            {
                _context.Categories.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
