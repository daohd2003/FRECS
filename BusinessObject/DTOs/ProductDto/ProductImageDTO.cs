using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ProductDto
{
    public class ProductImageDTO
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string ImageUrl { get; set; }

        public bool IsPrimary { get; set; }
    }
}
