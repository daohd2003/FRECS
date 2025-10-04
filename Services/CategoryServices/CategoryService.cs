using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using DataAccess;
using Repositories.CategoryRepositories;
using BusinessObject.Utilities;
using Services.CloudServices;

namespace Services.CategoryServices
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repository;
        private readonly IMapper _mapper;
        protected readonly ShareItDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public CategoryService(ICategoryRepository repository, IMapper mapper, ShareItDbContext context, ICloudinaryService cloudinaryService)
        {
            _repository = repository;
            _mapper = mapper;
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllAsync()
        {
            var entities = await _repository.GetAllCategoryAsync();
            return _mapper.Map<IEnumerable<CategoryDto>>(entities);
        }

        public async Task<CategoryDto?> GetByIdAsync(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<CategoryDto>(entity);
        }

        public async Task<CategoryDto?> GetByNameAsync(string name)
        {
            var entity = await _repository.GetByNameAsync(name);
            return entity == null ? null : _mapper.Map<CategoryDto>(entity);
        }

        public async Task<CategoryDto> CreateAsync(CategoryCreateUpdateDto dto, Guid userId)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                throw new ArgumentException("Category name is required");
            }

            var entity = new Category
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive,
                CreatedAt = DateTimeHelper.GetVietnamTime()
            };

            // Handle ImageUrl - it should already be uploaded via CategoryUploadController
            // So we just need to save the URL directly
            if (!string.IsNullOrEmpty(dto.ImageUrl))
            {
                var imageUrl = dto.ImageUrl.Trim();
                
                // Validate that it's a valid HTTP/HTTPS URL
                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && 
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    entity.ImageUrl = imageUrl;
                }
                else
                {
                    // For now, just store whatever URL is provided
                    // In production, you should enforce upload via API first
                    entity.ImageUrl = imageUrl;
                }
            }

            try
            {
                await _repository.AddCategoryAsync(entity);
                return _mapper.Map<CategoryDto>(entity);
            }
            catch (Exception ex)
            {
                // Log detailed error information
                var errorMessage = $"Failed to save category to database. " +
                    $"Entity: Id={entity.Id}, Name={entity.Name}, Description={entity.Description}, " +
                    $"IsActive={entity.IsActive}, ImageUrl={entity.ImageUrl}, CreatedAt={entity.CreatedAt}";
                
                // Get inner exception details
                var innerException = ex.InnerException;
                var innerMessage = innerException?.Message ?? "No inner exception";
                
                throw new Exception($"{errorMessage}. Inner exception: {innerMessage}. Full exception: {ex}", ex);
            }
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



