using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.PagingDto;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Services.ProductServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/products")]
    [ApiController]
    [Authorize(Roles = "admin,provider")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _service;
        private readonly IMapper _mapper;

        public ProductController(IProductService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _service.GetByIdAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpGet()]
        [AllowAnonymous]
        public  IActionResult GetAll()
        {
            IQueryable<ProductDTO> products = _service.GetAll();
            if (products == null) return NotFound();
            return Ok(products);
        }

        [HttpGet("filter")] 
        public async Task<ActionResult<PagedResult<ProductDTO>>> GetProductsAsync(
            [FromQuery] string? searchTerm,
            [FromQuery] string status,
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 5) 
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 5; 

            if (searchTerm == "\"\"")
            {
                searchTerm = string.Empty;
            }

            IQueryable<ProductDTO> products = _service.GetAll(); 

            var query = products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(lowerSearchTerm) ||
                    (p.Description != null && p.Description.ToLower().Contains(lowerSearchTerm))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
            {
                var lowerStatus = status.ToLower();
                query = query.Where(p =>
                    p.AvailabilityStatus.ToLower().Equals(lowerStatus)
                );
            }

            var totalCount = query.Count();

            var items = query
                .OrderByDescending(p => p.CreatedAt) 
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(); 

            var pagedResult = new PagedResult<ProductDTO>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            return Ok(pagedResult);
        }



        /* [HttpPost]
         public async Task<IActionResult> Create([FromBody] ProductDTO dto)
         {
             var created = await _service.AddAsync(dto);
             return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
         }*/
        [HttpPost]
        [Authorize] // Đảm bảo người dùng đã đăng nhập
        public async Task<IActionResult> Create([FromBody] ProductRequestDTO dto)
        {
            try
            {
                // Lấy thông tin Provider từ token
                dto.ProviderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                /*  dto.ProviderName = User.FindFirstValue(ClaimTypes.Name); // Hoặc một claim khác chứa tên*/

                var createdProduct = await _service.AddAsync(dto);

                // Sử dụng AutoMapper để map Product entity trả về thành ProductDTO để hiển thị
                // var resultDto = _mapper.Map<ProductDTO>(createdProduct);

                // Hoặc trả về chính object đã tạo
                return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, createdProduct);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpPut("{id}")]
        [Authorize] // Đảm bảo người dùng đã đăng nhập
        public async Task<IActionResult> Update(Guid id, [FromBody] ProductRequestDTO dto)
        {
            try
            {
                // Đảm bảo ProviderId từ token
                dto.ProviderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Convert ProductRequestDTO to ProductDTO để update
                var productDto = new ProductDTO
                {
                    Id = id,
                    ProviderId = dto.ProviderId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    Category = dto.Category,
                    Size = dto.Size,
                    Color = dto.Color,
                    PricePerDay = dto.PricePerDay,
                    PurchasePrice = dto.PurchasePrice ?? 0,
                    PurchaseQuantity = dto.PurchaseQuantity ?? 0,
                    RentalQuantity = dto.RentalQuantity ?? 0,
                    Gender = dto.Gender,
                    RentalStatus = dto.RentalStatus,
                    PurchaseStatus = dto.PurchaseStatus,
                    Images = dto.Images
                };

                var result = await _service.UpdateAsync(productDto);
                if (!result) return NotFound("Product not found or update failed.");
                
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpPut("update-status/{id}")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] ProductStatusUpdateDto request)
        {
            if (id != request.ProductId)
            {
                return BadRequest("Product ID in route does not match Product ID in request body.");
            }

            var result = await _service.UpdateProductStatusAsync(
               request
            );

            if (!result)
            {
                return NotFound("Product not found or status update failed.");
            }

            return NoContent();
        }

        [HttpGet("by-type/{productType}")]
        [AllowAnonymous]
        public IActionResult GetByProductType(string productType)
        {
            var products = _service.GetAll();
            
            var filteredProducts = productType.ToUpper() switch
            {
                "BOTH" => products.Where(p => p.ProductType == "BOTH"),
                "RENTAL" => products.Where(p => p.ProductType == "RENTAL"),
                "PURCHASE" => products.Where(p => p.ProductType == "PURCHASE"),
                "UNAVAILABLE" => products.Where(p => p.ProductType == "UNAVAILABLE"),
                _ => products
            };

            var result = filteredProducts.Select(p => new
            {
                p.Id,
                p.Name,
                p.ProductType,
                p.PricePerDay,
                p.PurchasePrice,
                p.SecurityDeposit,
                IsRentalAvailable = p.IsRentalAvailable,
                IsPurchaseAvailable = p.IsPurchaseAvailable,
                PrimaryPrice = p.GetPrimaryPriceDisplay(),
                Stats = p.GetStatsDisplay(),
                Deposit = p.GetDepositDisplay()
            }).ToList();

            return Ok(new
            {
                ProductType = productType.ToUpper(),
                Count = result.Count,
                Products = result
            });
        }

        [HttpDelete("{id}")]
        [Authorize] // Chỉ provider được xóa sản phẩm của mình
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Get product để check ownership và business rules
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");
                
                // Check ownership
                if (product.ProviderId != providerId)
                    return Forbid("You can only delete your own products.");
                
                // Kiểm tra xem product có tồn tại trong OrderItem không
                var hasOrderItems = await _service.HasOrderItemsAsync(id);
                
                // Business logic cho delete - implement smart delete logic
                string action;
                string message;
                
                // Kiểm tra điều kiện xóa: không có trong OrderItem và cả RentCount và BuyCount đều = 0
                if (hasOrderItems || product.RentCount > 0 || product.BuyCount > 0)
                {
                    // Có lịch sử giao dịch - chỉ chuyển status thành archived
                    product.AvailabilityStatus = "archived";
                    var updateResult = await _service.UpdateAsync(product);
                    
                    if (!updateResult)
                        return BadRequest(new ApiResponse<string>("Failed to archive product.", null));
                    
                    action = "Archived";
                    var reasons = new List<string>();
                    if (hasOrderItems) reasons.Add("exists in orders");
                    if (product.RentCount > 0) reasons.Add($"{product.RentCount} rental transactions");
                    if (product.BuyCount > 0) reasons.Add($"{product.BuyCount} purchase transactions");
                    
                    message = $"Product archived due to: {string.Join(", ", reasons)}.";
                }
                else
                {
                    // Không có lịch sử giao dịch - có thể xóa vĩnh viễn
                    var deleteResult = await _service.DeleteAsync(id);
                    
                    if (!deleteResult)
                        return BadRequest(new ApiResponse<string>("Failed to delete product.", null));
                    
                    action = "Permanently Deleted";
                    message = "Product has been permanently removed.";
                }
                
                return Ok(new ApiResponse<string>(message, action));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }

        [HttpPut("restore/{id}")]
        [Authorize] // Chỉ provider được restore sản phẩm của mình
        public async Task<IActionResult> RestoreProduct(Guid id)
        {
            try
            {
                var providerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Get product để check ownership
                var product = await _service.GetByIdAsync(id);
                if (product == null) 
                    return NotFound("Product not found.");
                
                // Check ownership
                if (product.ProviderId != providerId)
                    return Forbid("You can only restore your own products.");
                
                // Check if product can be restored
                if (product.AvailabilityStatus != "archived" && product.AvailabilityStatus != "deleted")
                    return BadRequest("Only archived or deleted products can be restored.");
                
                // Restore product - change status to available
                product.AvailabilityStatus = "available";
                var result = await _service.UpdateAsync(product);
                
                if (!result)
                    return BadRequest("Failed to restore product.");
                
                return Ok(new ApiResponse<string>("Product has been restored to active status.", "Restored"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(ex.Message, null));
            }
        }
    }
}
