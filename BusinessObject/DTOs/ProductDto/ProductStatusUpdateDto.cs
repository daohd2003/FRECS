using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ProductDto
{
    public class ProductStatusUpdateDto
    {
        public Guid ProductId { get; set; } // Matches "productId" in JS
        public string NewAvailabilityStatus { get; set; } // Matches "newAvailabilityStatus" in JS
    }
}
