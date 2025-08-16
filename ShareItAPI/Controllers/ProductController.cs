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
        public async Task<IActionResult> Update([FromBody] ProductDTO dto)
        {
            var result = await _service.UpdateAsync(dto);
            if (!result) return NotFound();
            return NoContent();
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        // Debug endpoint to check rent count

    }
}
