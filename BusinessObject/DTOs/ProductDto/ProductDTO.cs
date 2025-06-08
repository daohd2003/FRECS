using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ProductDto
{
    public class ProductDTO
    {
        public Guid Id { get; set; }

        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public string? Size { get; set; }

        public string? Color { get; set; }

        public decimal PricePerDay { get; set; }

        public string AvailabilityStatus { get; set; }

        public bool IsPromoted { get; set; }

        public int RentCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string PrimaryImagesUrl { get; set; }

        // Optional: include all images if needed
        public List<ProductImageDTO>? Images { get; set; }
    }
}
