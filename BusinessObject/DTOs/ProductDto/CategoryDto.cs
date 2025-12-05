using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.ProductDto
{
	public class CategoryDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public bool IsActive { get; set; }
		public string? ImageUrl { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public ICollection<ProductDTO> Products { get; set; } = new List<ProductDTO>();
	}

	public class CategoryCreateUpdateDto
	{
		[Required]
		[MaxLength(150)]
		public string Name { get; set; }

		[Required]
		[MaxLength(255)]
		public string Description { get; set; }

		[Required(ErrorMessage = "Category image is required")]
		public string ImageUrl { get; set; }
		
		public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

	/// <summary>
	/// Lightweight DTO for category with product count only (optimized for Home page)
	/// </summary>
	public class CategoryWithProductCountDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public string? ImageUrl { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public int ActiveProductCount { get; set; }
	}
}



