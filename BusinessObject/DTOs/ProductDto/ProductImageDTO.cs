namespace BusinessObject.DTOs.ProductDto
{
    public class ProductImageDTO
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string ImageUrl { get; set; }
        /* public string PublicId { get; set; }*/
        public bool IsPrimary { get; set; }
    }
}
