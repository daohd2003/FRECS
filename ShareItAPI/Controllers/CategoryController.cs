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
	/// Creates a new category with required image upload
	/// </summary>
	/// <param name="name">Category name (required)</param>
	/// <param name="description">Category description (optional)</param>
	/// <param name="isActive">Is category active (default: true)</param>
	/// <param name="ImageFile">Category image file (required)</param>
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
	/// - ImageFile: [file] (required - JPG, PNG, GIF, WEBP, max 5MB)
	/// 
	/// Image will be uploaded to Cloudinary automatically and URL saved to database.
	/// </remarks>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromForm] string name, 
		[FromForm] IFormFile? ImageFile,
		[FromForm] string description, 
		[FromForm] bool isActive = true)
	{
		try
		{
			// Validate required fields
			if (string.IsNullOrWhiteSpace(name))
			{
				return BadRequest(new ApiResponse<string>("Category name is required", null));
			}

			// Validate description is provided
			if (string.IsNullOrWhiteSpace(description))
			{
				return BadRequest(new ApiResponse<string>("Category description is required", null));
			}

			// Validate image is provided
			if (ImageFile == null || ImageFile.Length == 0)
			{
				return BadRequest(new ApiResponse<string>("Image is required", null));
			}

			// Get user ID from JWT token
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
			{
				return BadRequest(new ApiResponse<string>("Invalid user ID", null));
			}

			// Upload image to Cloudinary (required)
			string imageUrl;
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

			// Create DTO with uploaded image URL
			var dto = new CategoryCreateUpdateDto
			{
				Name = name,
				Description = description,
				IsActive = isActive,
				ImageUrl = imageUrl  // Cloudinary URL (required)
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
		[FromForm] string description,
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

			// Validate description is provided
			if (string.IsNullOrWhiteSpace(description))
			{
				return BadRequest(new ApiResponse<string>("Category description is required", null));
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
					// Delete old image from Cloudinary if exists
					if (!string.IsNullOrEmpty(existing.ImageUrl))
					{
						try
						{
							var oldPublicId = ExtractPublicIdFromUrl(existing.ImageUrl);
							if (!string.IsNullOrEmpty(oldPublicId))
							{
								await _cloudinaryService.DeleteImageAsync(oldPublicId);
							}
						}
						catch (Exception ex)
						{
							// Log but don't fail if old image deletion fails
							Console.WriteLine($"Failed to delete old image: {ex.Message}");
						}
					}

					// Upload new image
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

			// Delete image from Cloudinary if exists
			if (!string.IsNullOrEmpty(category.ImageUrl))
			{
				try
				{
					var publicId = ExtractPublicIdFromUrl(category.ImageUrl);
					if (!string.IsNullOrEmpty(publicId))
					{
						await _cloudinaryService.DeleteImageAsync(publicId);
					}
				}
				catch (Exception ex)
				{
					// Log but don't fail the deletion if image removal fails
					Console.WriteLine($"Failed to delete Cloudinary image: {ex.Message}");
				}
			}

			// Delete category from database
			var ok = await _service.DeleteAsync(id);
			if (!ok) return NotFound(new ApiResponse<string>("Failed to delete category", null));
			
			return Ok(new ApiResponse<object>("Category deleted successfully", new { id }));
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse<string>($"Failed to delete category: {ex.Message}", null));
		}
	}

	/// <summary>
	/// Extracts Cloudinary public ID from full URL
	/// </summary>
	/// <param name="imageUrl">Full Cloudinary URL</param>
	/// <returns>Public ID or null if extraction fails</returns>
	/// <example>
	/// Input: https://res.cloudinary.com/xxx/image/upload/v123/ShareIt/categories/user123/image.jpg
	/// Output: ShareIt/categories/user123/image
	/// </example>
	private string? ExtractPublicIdFromUrl(string imageUrl)
	{
		if (string.IsNullOrEmpty(imageUrl)) return null;

		try
		{
			// Cloudinary URL format: .../upload/v[version]/[publicId].[extension]
			var uri = new Uri(imageUrl);
			var path = uri.AbsolutePath;
			
			// Find the upload segment
			var uploadIndex = path.IndexOf("/upload/");
			if (uploadIndex == -1) return null;
			
			// Get everything after /upload/v[version]/
			var afterUpload = path.Substring(uploadIndex + "/upload/".Length);
			
			// Skip version (v123456789)
			var versionEndIndex = afterUpload.IndexOf('/');
			if (versionEndIndex == -1) return null;
			
			var publicIdWithExtension = afterUpload.Substring(versionEndIndex + 1);
			
			// Remove file extension
			var lastDotIndex = publicIdWithExtension.LastIndexOf('.');
			if (lastDotIndex != -1)
			{
				return publicIdWithExtension.Substring(0, lastDotIndex);
			}
			
			return publicIdWithExtension;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to extract public ID from URL: {ex.Message}");
			return null;
		}
	}
	}
}
