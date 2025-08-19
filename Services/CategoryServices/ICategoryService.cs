using BusinessObject.DTOs.ProductDto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services.CategoryServices
{
	public interface ICategoryService
	{
		Task<IEnumerable<CategoryDto>> GetAllAsync();
		Task<CategoryDto?> GetByIdAsync(Guid id);
		Task<CategoryDto> CreateAsync(CategoryCreateUpdateDto dto);
		Task<bool> UpdateAsync(Guid id, CategoryCreateUpdateDto dto);
		Task<bool> DeleteAsync(Guid id);
	}
}



