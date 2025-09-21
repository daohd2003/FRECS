using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;


namespace Services.ProductServices
{
    public class ProductService : IProductService
    {
        private readonly ShareItDbContext _context;
        private readonly IMapper _mapper;
        public ProductService(ShareItDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public IQueryable<ProductDTO> GetAll()
        {
            return _context.Products
                .Include(p => p.Category)
                .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider);
        }

        public async Task<ProductDTO?> GetByIdAsync(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Provider)
                .ThenInclude(prov => prov.Profile)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            return product == null ? null : _mapper.Map<ProductDTO>(product);
        }



        /*public async Task<ProductDTO> AddAsync(ProductDTO productDto)
        {
            var product = _mapper.Map<Product>(productDto);
            product.Id = Guid.NewGuid();
            product.CreatedAt = DateTimeHelper.GetVietnamTime();

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            return _mapper.Map<ProductDTO>(product);
        }*/
        public async Task<ProductDTO> AddAsync(ProductRequestDTO dto)
        {
            // Bắt đầu transaction để đảm bảo toàn vẹn dữ liệu
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bước 1: Tạo đối tượng Product Entity từ DTO
                var newProduct = new Product
                {
                    ProviderId = dto.ProviderId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId ?? Guid.Empty,
                    Size = dto.Size,
                    Color = dto.Color,
                    PricePerDay = dto.PricePerDay,
                    PurchasePrice = dto.PurchasePrice ?? 0,
                    PurchaseQuantity = dto.PurchaseQuantity ?? 0,
                    RentalQuantity = dto.RentalQuantity ?? 0,
                    CreatedAt = DateTimeHelper.GetVietnamTime(),
                    AvailabilityStatus = AvailabilityStatus.available,
                    UpdatedAt = DateTimeHelper.GetVietnamTime(),
                    AverageRating = 0,
                    RatingCount = 0,
                    IsPromoted = false
                };

                // Bước 2: Thêm Product vào DbContext và Lưu để lấy Id
                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync(); // <-- DB sẽ sinh ra Id cho newProduct tại đây

                // Bước 3: Dùng newProduct.Id để tạo các bản ghi ProductImage
                if (dto.Images != null && dto.Images.Any())
                {
                    foreach (var imageDto in dto.Images)
                    {
                        var productImage = new ProductImage
                        {
                            ProductId = newProduct.Id, // <-- Dùng Id vừa được tạo
                            ImageUrl = imageDto.ImageUrl,
                            IsPrimary = imageDto.IsPrimary,
                            // Nếu bạn đã thêm cột PublicId, hãy gán nó ở đây
                            // PublicId = imageDto.PublicId 
                        };
                        _context.ProductImages.Add(productImage);
                    }

                    // Lưu tất cả các ảnh vào DB
                    await _context.SaveChangesAsync();
                }

                // Bước 4: Nếu mọi thứ thành công, commit transaction
                await transaction.CommitAsync();

                return _mapper.Map<ProductDTO>(newProduct);
            }
            catch (Exception)
            {
                // Nếu có lỗi, rollback tất cả
                await transaction.RollbackAsync();
                throw; // Ném lại lỗi để Controller bắt và xử lý
            }
        }

        public async Task<bool> UpdateAsync(ProductDTO productDto)
        {
            var existingProduct = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productDto.Id);

            if (existingProduct == null) return false;

            // Map các trường từ DTO sang entity, giữ lại các trường không map nếu cần
            _mapper.Map(productDto, existingProduct);

            existingProduct.UpdatedAt = DateTimeHelper.GetVietnamTime();

            _context.Products.Update(existingProduct);
            var updated = await _context.SaveChangesAsync();

            return updated > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            _context.Products.Remove(product);
            var deleted = await _context.SaveChangesAsync();

            return deleted > 0;
        }

        public async Task<bool> UpdateProductStatusAsync(ProductStatusUpdateDto request)
        {
            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null) return false;

            product.UpdatedAt = DateTimeHelper.GetVietnamTime();
            if (request.NewAvailabilityStatus.Equals("Approved"))
            {
                product.AvailabilityStatus = AvailabilityStatus.available;

            }
            else
            {
                product.AvailabilityStatus = AvailabilityStatus.rejected;
            }
            _context.Products.Update(product);
            var deleted = await _context.SaveChangesAsync();
            return true;
        }
    }
}
