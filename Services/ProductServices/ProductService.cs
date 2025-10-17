using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.ProductDto;
using Repositories.ProductRepositories;


namespace Services.ProductServices
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        public ProductService(IProductRepository productRepository, IMapper mapper)
        {
            _productRepository = productRepository;
            _mapper = mapper;
        }

        public IQueryable<ProductDTO> GetAll()
        {
            return _productRepository.GetAllWithIncludes()
                .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider);
        }

        public async Task<ProductDTO?> GetByIdAsync(Guid id)
        {
            var product = await _productRepository.GetProductWithImagesByIdAsync(id);
            return product == null ? null : _mapper.Map<ProductDTO>(product);
        }



        public async Task<ProductDTO> AddAsync(ProductRequestDTO dto)
        {
            var newProduct = await _productRepository.AddProductWithImagesAsync(dto);
            return _mapper.Map<ProductDTO>(newProduct);
        }

        public async Task<bool> UpdateAsync(ProductDTO productDto)
        {
            return await _productRepository.UpdateProductWithImagesAsync(productDto);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            return await _productRepository.DeleteAsync(id);
        }

        public async Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request)
        {
            return await _productRepository.UpdateProductStatusAsync(request);
        }

        public async Task<bool> HasOrderItemsAsync(Guid productId)
        {
            return await _productRepository.HasOrderItemsAsync(productId);
        }
    }
}
