using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.CategoryRepositories
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<List<Category>> GetAllCategoryAsync();
        Task<Category?> GetByNameAsync(string name);

        Task AddCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(Guid id);
    }
}



