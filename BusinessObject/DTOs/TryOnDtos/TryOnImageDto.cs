namespace BusinessObject.DTOs.TryOnDtos
{
    public class TryOnImageDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? PersonImageUrl { get; set; }
        public string? GarmentImageUrl { get; set; }
        public string? ClothingType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class SaveTryOnImageRequest
    {
        public Guid? ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string CloudinaryPublicId { get; set; } = string.Empty;
        public string? PersonImageUrl { get; set; }
        public string? PersonPublicId { get; set; }
        public string? GarmentImageUrl { get; set; }
        public string? GarmentPublicId { get; set; }
        public string? ClothingType { get; set; }
    }

    public class TryOnImageListResponse
    {
        public List<TryOnImageDto> Images { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
