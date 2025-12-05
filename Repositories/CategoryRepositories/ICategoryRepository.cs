using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.CategoryRepositories
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<List<Category>> GetAllCategoryAsync();
        Task<List<Category>> GetAllCategoryWithActiveProductsAsync();
        Task<List<CategoryWithProductCountDto>> GetAllCategoryWithActiveProductCountAsync();
        Task<Category?> GetByNameAsync(string name);

        Task AddCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(Guid id);
    }
}



