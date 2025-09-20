using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using DataAccess;
using Repositories.CategoryRepositories;
using BusinessObject.Utilities;

namespace Services.CategoryServices
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repository;
        private readonly IMapper _mapper;
        protected readonly ShareItDbContext _context;

        public CategoryService(ICategoryRepository repository, IMapper mapper, ShareItDbContext context)
        {
            _repository = repository;
            _mapper = mapper;
            _context = context;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllAsync()
        {
            var entities = await _repository.GetAllAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(entities);
        }

        public async Task<CategoryDto?> GetByIdAsync(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<CategoryDto>(entity);
        }

        public async Task<CategoryDto> CreateAsync(CategoryCreateUpdateDto dto)
        {
            var entity = _mapper.Map<Category>(dto);
            entity.Id = Guid.NewGuid();
            await _repository.AddCategoryAsync(entity);
            return _mapper.Map<CategoryDto>(entity);
        }

        public async Task<bool> UpdateAsync(Guid id, CategoryCreateUpdateDto dto)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null) return false;

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTimeHelper.GetVietnamTime();

            await _repository.UpdateCategoryAsync(existing);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null) return false;

            await _repository.DeleteCategoryAsync(id);
            return true;
        }

    }
}



