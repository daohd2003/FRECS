using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.Models
{
	public class Category
	{
		[Key]
		public Guid Id { get; set; }

		[Required]
		[MaxLength(150)]
		public string Name { get; set; }

		[MaxLength(255)]
		public string Description { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }

		public ICollection<Product> Products { get; set; } = new List<Product>();
	}
}



