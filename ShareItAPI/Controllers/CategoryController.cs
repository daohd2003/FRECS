using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CategoryServices;
using Services.CloudServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
	[Route("api/categories")]
	[ApiController]
	[Authorize(Roles = "admin,staff")]
	public class CategoryController : ControllerBase
	{
		private readonly ICategoryService _service;
		private readonly ICloudinaryService _cloudinaryService;

		public CategoryController(ICategoryService service, ICloudinaryService cloudinaryService)
		{
			_service = service;
			_cloudinaryService = cloudinaryService;
		}

	/// <summary>
	/// Upload category image to Cloudinary
	/// </summary>
	/// <param name="image">Image file to upload</param>
	/// <returns>Image URL and Public ID</returns>
	[HttpPost("upload-image")]
	public async Task<IActionResult> UploadCategoryImage([FromForm] IFormFile image)
	{
		if (image == null || image.Length == 0)
		{
			return BadRequest(new ApiResponse<string>("No image provided.", null));
		}

		try
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
			{
				return BadRequest(new ApiResponse<string>("Invalid user ID.", null));
			}
			
			// Use dedicated category upload method with validation
			var result = await _cloudinaryService.UploadCategoryImageAsync(image, userId);
			
			return Ok(new ApiResponse<ImageUploadResult>("Category image uploaded successfully.", result));
		}
		catch (ArgumentException ex)
		{
			// Validation errors (file type, size, etc.)
			return BadRequest(new ApiResponse<string>($"Validation error: {ex.Message}", null));
		}
		catch (Exception ex)
		{
			// Other errors (Cloudinary upload failed, etc.)
			return StatusCode(500, new ApiResponse<string>($"Upload failed: {ex.Message}", null));
		}
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

	/// <summary>
	/// Creates a new category with optional image upload
	/// </summary>
	/// <param name="name">Category name (required)</param>
	/// <param name="description">Category description (optional)</param>
	/// <param name="isActive">Is category active (default: true)</param>
	/// <param name="image">Category image file (optional)</param>
	/// <returns>Created category with uploaded image URL</returns>
	/// <remarks>
	/// Sample request:
	/// POST /api/categories
	/// Content-Type: multipart/form-data
	/// 
	/// Form fields:
	/// - name: "Electronics" (required)
	/// - description: "Electronic devices" (optional)
	/// - isActive: true (optional, default: true)
	/// - image: [file] (optional - JPG, PNG, GIF, WEBP, max 5MB)
	/// 
	/// Image will be uploaded to Cloudinary automatically and URL saved to database.
	/// </remarks>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromForm] string name, 
		[FromForm] string? description, 
		[FromForm] bool isActive = true, 
		[FromForm] IFormFile? ImageFile = null)
	{
		try
		{
			// Validate required fields
			if (string.IsNullOrWhiteSpace(name))
			{
				return BadRequest(new ApiResponse<string>("Category name is required", null));
			}

			// Get user ID from JWT token
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
			{
				return BadRequest(new ApiResponse<string>("Invalid user ID", null));
			}

			// Upload image to Cloudinary if provided
			string? imageUrl = null;
			if (ImageFile != null && ImageFile.Length > 0)
			{
				try
				{
					var uploadResult = await _cloudinaryService.UploadCategoryImageAsync(ImageFile, userId);
					imageUrl = uploadResult.ImageUrl;
				}
				catch (ArgumentException ex)
				{
					// Validation error from upload (file type, size, etc.)
					return BadRequest(new ApiResponse<string>($"Image upload validation failed: {ex.Message}", null));
				}
				catch (Exception ex)
				{
					// Upload error
					return StatusCode(500, new ApiResponse<string>($"Image upload failed: {ex.Message}", null));
				}
			}

			// Create DTO with uploaded image URL
			var dto = new CategoryCreateUpdateDto
			{
				Name = name,
				Description = description,
				IsActive = isActive,
				ImageUrl = imageUrl  // Cloudinary URL or null
			};

			// Create category in database
			var created = await _service.CreateAsync(dto, userId);
			
			return CreatedAtAction(nameof(GetById), new { id = created.Id }, 
				new ApiResponse<CategoryDto>("Category created successfully", created));
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse<string>($"Failed to create category: {ex.Message}", null));
		}
	}

	[HttpPatch("{id}/status")]
	public async Task<IActionResult> ToggleStatus(Guid id, [FromQuery] bool isActive)
	{
		try
		{
			var category = await _service.GetByIdAsync(id);
			if (category == null)
			{
				return NotFound(new ApiResponse<string>("Category not found", null));
			}

			var dto = new CategoryCreateUpdateDto
			{
				Name = category.Name,
				Description = category.Description,
				IsActive = isActive,
				ImageUrl = category.ImageUrl
			};

			var ok = await _service.UpdateAsync(id, dto);
			if (!ok) return NotFound();
			
			return Ok(new ApiResponse<object>("Category status updated successfully", new { id, isActive }));
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse<string>($"Failed to update status: {ex.Message}", null));
		}
	}

	[HttpPut("{id}")]
	public async Task<IActionResult> Update(
		Guid id,
		[FromForm] string name,
		[FromForm] string? description,
		[FromForm] bool isActive = true,
		[FromForm] IFormFile? ImageFile = null)
	{
		try
		{
			// Validate required fields
			if (string.IsNullOrWhiteSpace(name))
			{
				return BadRequest(new ApiResponse<string>("Category name is required", null));
			}

			// Get user ID from JWT token
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
			{
				return BadRequest(new ApiResponse<string>("Invalid user ID", null));
			}

			// Get existing category
			var existing = await _service.GetByIdAsync(id);
			if (existing == null)
			{
				return NotFound(new ApiResponse<string>("Category not found", null));
			}

			// Upload new image if provided, otherwise keep existing
			string? imageUrl = existing.ImageUrl;
			if (ImageFile != null && ImageFile.Length > 0)
			{
				try
				{
					var uploadResult = await _cloudinaryService.UploadCategoryImageAsync(ImageFile, userId);
					imageUrl = uploadResult.ImageUrl;
				}
				catch (ArgumentException ex)
				{
					return BadRequest(new ApiResponse<string>($"Image upload validation failed: {ex.Message}", null));
				}
				catch (Exception ex)
				{
					return StatusCode(500, new ApiResponse<string>($"Image upload failed: {ex.Message}", null));
				}
			}

			// Create DTO with updated values
			var dto = new CategoryCreateUpdateDto
			{
				Name = name,
				Description = description,
				IsActive = isActive,
				ImageUrl = imageUrl
			};

			// Update category in database
			var ok = await _service.UpdateAsync(id, dto);
			if (!ok) return NotFound();

			return Ok(new ApiResponse<object>("Category updated successfully", new { id, name, isActive, imageUrl }));
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse<string>($"Failed to update category: {ex.Message}", null));
		}
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(Guid id)
	{
		try
		{
			// Get category with products to check
			var category = await _service.GetByIdAsync(id);
			if (category == null)
			{
				return NotFound(new ApiResponse<string>("Category not found", null));
			}

			// Check if category has products
			if (category.Products != null && category.Products.Count > 0)
			{
				return BadRequest(new ApiResponse<string>(
					$"Cannot delete category. It contains {category.Products.Count} product(s). Please remove or reassign products first.", 
					null));
			}

			// Delete category
			var ok = await _service.DeleteAsync(id);
			if (!ok) return NotFound(new ApiResponse<string>("Failed to delete category", null));
			
			return Ok(new ApiResponse<object>("Category deleted successfully", new { id }));
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse<string>($"Failed to delete category: {ex.Message}", null));
		}
	}
	}
}



