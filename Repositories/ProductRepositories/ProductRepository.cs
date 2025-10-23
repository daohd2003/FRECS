using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.ProductRepositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(ShareItDbContext context) : base(context)
        {
        }

        public IQueryable<Product> GetAllWithIncludes()
        {
            return _context.Products.AsNoTracking();
        }

        public async Task<IEnumerable<Product>> GetProductsWithImagesAsync()
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
            .ToListAsync();
        }

        public async Task<Product?> GetProductWithImagesByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Provider)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> IsProductAvailable(Guid productId, DateTime startDate, DateTime endDate)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.AvailabilityStatus != AvailabilityStatus.available)
            {
                return false;
            }

            var conflictingOrders = await _context.Orders
                .Where(o =>
                    o.Items.Any(oi => oi.ProductId == productId) &&
                    (
                        // Check for overlapping date ranges
                        (startDate < o.RentalEnd && endDate > o.RentalStart)
                    ) &&
                    (
                        o.Status != OrderStatus.cancelled &&
                        o.Status != OrderStatus.returned
                    )
                )
                .AnyAsync();

            return !conflictingOrders;
        }

        public async Task<Product> AddProductWithImagesAsync(ProductRequestDTO dto)
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
                    SecurityDeposit = dto.SecurityDeposit,
                    // Map 3 fields mới từ string sang enum
                    Gender = Enum.Parse<Gender>(dto.Gender ?? "Unisex"),
                    RentalStatus = Enum.Parse<RentalStatus>(dto.RentalStatus ?? "Available"),
                    PurchaseStatus = Enum.Parse<PurchaseStatus>(dto.PurchaseStatus ?? "NotForSale"),
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

                return newProduct;
            }
            catch (Exception)
            {
                // Nếu có lỗi, rollback tất cả
                await transaction.RollbackAsync();
                throw; // Ném lại lỗi để Service/Controller bắt và xử lý
            }
        }

        public async Task<bool> UpdateProductWithImagesAsync(ProductDTO productDto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingProduct = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productDto.Id);

                if (existingProduct == null) return false;

                // Cập nhật các trường từ DTO
                existingProduct.Name = productDto.Name;
                existingProduct.Description = productDto.Description;
                existingProduct.CategoryId = productDto.CategoryId ?? Guid.Empty;
                existingProduct.Size = productDto.Size;
                existingProduct.Color = productDto.Color;
                existingProduct.PricePerDay = productDto.PricePerDay;
                existingProduct.PurchasePrice = productDto.PurchasePrice;
                existingProduct.PurchaseQuantity = productDto.PurchaseQuantity;
                existingProduct.RentalQuantity = productDto.RentalQuantity;
                existingProduct.SecurityDeposit = productDto.SecurityDeposit;
                
                // Parse string sang enum
                existingProduct.Gender = Enum.Parse<Gender>(productDto.Gender ?? "Unisex");
                existingProduct.RentalStatus = Enum.Parse<RentalStatus>(productDto.RentalStatus ?? "Available");
                existingProduct.PurchaseStatus = Enum.Parse<PurchaseStatus>(productDto.PurchaseStatus ?? "NotForSale");
                existingProduct.AvailabilityStatus = Enum.Parse<AvailabilityStatus>(productDto.AvailabilityStatus ?? "available");
                
                existingProduct.UpdatedAt = DateTimeHelper.GetVietnamTime();

                // Xử lý Images riêng biệt
                if (productDto.Images != null)
                {
                    // Xóa tất cả images cũ
                    _context.ProductImages.RemoveRange(existingProduct.Images);
                    
                    // Thêm images mới
                    foreach (var imageDto in productDto.Images)
                    {
                        var productImage = new ProductImage
                        {
                            ProductId = existingProduct.Id,
                            ImageUrl = imageDto.ImageUrl,
                            IsPrimary = imageDto.IsPrimary
                        };
                        _context.ProductImages.Add(productImage);
                    }
                }

                _context.Products.Update(existingProduct);
                var updated = await _context.SaveChangesAsync();

                if (updated > 0)
                {
                    await transaction.CommitAsync();
                    return true;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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
            await _context.SaveChangesAsync();
            
            return true;
        }

        public async Task<bool> HasOrderItemsAsync(Guid productId)
        {
            return await _context.OrderItems.AnyAsync(oi => oi.ProductId == productId);
        }

        public async Task<Product?> GetProductWithProviderByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Provider)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> UpdateProductAvailabilityStatusAsync(Guid productId, AvailabilityStatus status)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return false;

            product.AvailabilityStatus = status;
            product.UpdatedAt = DateTimeHelper.GetVietnamTime();
            
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            
            return true;
        }

        public async Task<Product?> GetProductWithImagesAndProviderAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Provider)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}
