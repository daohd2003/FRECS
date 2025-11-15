using BusinessObject.DTOs.ProductDto;

namespace Services.ProductServices
{
    public interface IProductService
    {
        IQueryable<ProductDTO> GetAll();
        IQueryable<ProductDTO> GetAllNoFilter();
        Task<ProductDTO?> GetByIdAsync(Guid id);

        /*Task<ProductDTO> AddAsync(ProductDTO productDto);*/
        Task<ProductDTO> AddAsync(ProductRequestDTO productDto);
        Task<bool> UpdateAsync(ProductDTO productDto);
        Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request);
        Task<bool> UpdateProductImagesAsync(Guid productId, List<ProductImageDTO> newImages);

        Task<bool> DeleteAsync(Guid id);
        Task<bool> HasOrderItemsAsync(Guid productId);
    }
}
