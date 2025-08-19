using System;

namespace BusinessObject.DTOs.ProductDto
{
	public class CategoryDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}

	public class CategoryCreateUpdateDto
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public bool IsActive { get; set; } = true;
	}
}



