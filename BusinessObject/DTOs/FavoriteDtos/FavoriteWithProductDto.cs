using BusinessObject.DTOs.ProductDto;
using System;

namespace BusinessObject.DTOs.FavoriteDtos
{
    public class FavoriteWithProductDto
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ProductDTO Product { get; set; }
    }
}

