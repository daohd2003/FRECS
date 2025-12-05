using BusinessObject.DTOs.ProductDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.CategoryServices
{
	public interface ICategoryService
	{
		Task<IEnumerable<CategoryDto>> GetAllAsync();
		Task<IEnumerable<CategoryDto>> GetAllWithActiveProductsAsync();
		Task<IEnumerable<CategoryWithProductCountDto>> GetAllWithActiveProductCountAsync();
		Task<CategoryDto?> GetByIdAsync(Guid id);
		Task<CategoryDto?> GetByNameAsync(string name);
		Task<CategoryDto> CreateAsync(CategoryCreateUpdateDto dto, Guid userId);
		Task<bool> UpdateAsync(Guid id, CategoryCreateUpdateDto dto);
		Task<bool> DeleteAsync(Guid id);
	}
}



