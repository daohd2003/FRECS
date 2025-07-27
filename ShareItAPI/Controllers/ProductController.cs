using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
