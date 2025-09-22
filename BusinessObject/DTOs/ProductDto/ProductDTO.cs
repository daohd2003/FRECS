namespace BusinessObject.DTOs.ProductDto
{
    public class ProductDTO
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid? CategoryId { get; set; }
        public string Category { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal PurchasePrice { get; set; }
        public int PurchaseQuantity { get; set; }
        public int RentalQuantity { get; set; }
        public string AvailabilityStatus { get; set; }
        public bool IsPromoted { get; set; }
        public int RentCount { get; set; }
        public int BuyCount { get; set; }
        public string RentalStatus { get; set; }
        public string PurchaseStatus { get; set; }
        public string Gender { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string PrimaryImagesUrl { get; set; }
        public decimal AverageRating { get; set; }
        public List<ProductImageDTO>? Images { get; set; }
    }
    public class ProductRequestDTO
    {
        public Guid ProviderId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal? PurchasePrice { get; set; }
        public int? PurchaseQuantity { get; set; }
        public int? RentalQuantity { get; set; }
        public string? RentalStatus { get; set; }
        public string? PurchaseStatus { get; set; }
        public string? Gender { get; set; }
        public List<ProductImageDTO>? Images { get; set; }
    }
}
