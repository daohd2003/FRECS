using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CategoryServices;

namespace ShareItAPI.Controllers
{
	[Route("api/categories")]
	[ApiController]
	[Authorize(Roles = "admin")]
	public class CategoryController : ControllerBase
	{
		private readonly ICategoryService _service;

		public CategoryController(ICategoryService service)
		{
			_service = service;
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> GetAll()
		{
			var categories = await _service.GetAllAsync();
			return Ok(categories);
		}

		[HttpGet("{id}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetById(Guid id)
		{
			var category = await _service.GetByIdAsync(id);
			if (category == null) return NotFound();
			return Ok(category);
		}

		[HttpGet("by-name/{name}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetByName(string name)
		{
			var category = await _service.GetByNameAsync(name);
			if (category == null) return NotFound(new ApiResponse<string>($"Category '{name}' not found", null));
			return Ok(new ApiResponse<object>("Category found", category));
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CategoryCreateUpdateDto dto)
		{
			var created = await _service.CreateAsync(dto);
			return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> Update(Guid id, [FromBody] CategoryCreateUpdateDto dto)
		{
			var ok = await _service.UpdateAsync(id, dto);
			if (!ok) return NotFound();
			return NoContent();
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(Guid id)
		{
			var ok = await _service.DeleteAsync(id);
			if (!ok) return NotFound();
			return NoContent();
		}
	}
}



