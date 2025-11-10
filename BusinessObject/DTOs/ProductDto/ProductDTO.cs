using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.ProductDto
{
    public class ProductDTO
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string? ProviderEmail { get; set; }
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
        public decimal SecurityDeposit { get; set; }

        // Computed properties cho logic hiển thị
        public bool IsRentalAvailable => RentalStatus == "Available";
        public bool IsPurchaseAvailable => PurchaseStatus == "Available";
        
        // Pricing hiển thị
        public decimal? RentalPricePerDay => IsRentalAvailable ? PricePerDay : null;
        public decimal? PurchasePriceDisplay => IsPurchaseAvailable ? PurchasePrice : null;
        public decimal? SecurityDepositDisplay => IsRentalAvailable ? SecurityDeposit : null;
        
        // Statistics hiển thị
        public int? RentCountDisplay => IsRentalAvailable ? RentCount : null;
        public int? BuyCountDisplay => IsPurchaseAvailable ? BuyCount : null;
        
        // Availability hiển thị
        public int? RentalQuantityDisplay => IsRentalAvailable ? RentalQuantity : null;
        public int? PurchaseQuantityDisplay => IsPurchaseAvailable ? PurchaseQuantity : null;
        
        // Product type helper
        public string ProductType => GetProductType();
        
        private string GetProductType()
        {
            if (IsRentalAvailable && IsPurchaseAvailable)
                return "BOTH"; // Vừa thuê vừa bán
            if (IsRentalAvailable)
                return "RENTAL"; // Chỉ thuê
            if (IsPurchaseAvailable)
                return "PURCHASE"; // Chỉ bán
            return "UNAVAILABLE"; // Không khả dụng
        }

        // Display helpers cho price formatting
        public string GetPrimaryPriceDisplay()
        {
            switch (ProductType)
            {
                case "RENTAL":
                    return $"₫{RentalPricePerDay?.ToString("N0")}/day";
                case "PURCHASE":
                    return $"₫{PurchasePriceDisplay?.ToString("N0")}";
                case "BOTH":
                    return $"Rent: ₫{RentalPricePerDay?.ToString("N0")}/day | Buy: ₫{PurchasePriceDisplay?.ToString("N0")}";
                default:
                    return "Unavailable";
            }
        }

        public string GetStatsDisplay()
        {
            switch (ProductType)
            {
                case "RENTAL":
                    return $"Rented {RentCountDisplay} times";
                case "PURCHASE":
                    return $"Sold {BuyCountDisplay} times";
                case "BOTH":
                    return $"Rented {RentCountDisplay} times • Sold {BuyCountDisplay} times";
                default:
                    return "";
            }
        }

        public string GetDepositDisplay()
        {
            if (IsRentalAvailable && SecurityDepositDisplay > 0)
                return $"Deposit: ₫{SecurityDepositDisplay?.ToString("N0")}";
            return "";
        }
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
        public decimal SecurityDeposit { get; set; }
        public string? RentalStatus { get; set; }
        public string? PurchaseStatus { get; set; }
        public string? Gender { get; set; }
        public List<ProductImageDTO>? Images { get; set; }
    }

    /// <summary>
    /// DTO for Admin/Staff to update products without changing ProviderId
    /// </summary>
    public class AdminProductUpdateDTO
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(255, ErrorMessage = "Product name cannot be longer than 255 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(2000, ErrorMessage = "Description cannot be longer than 2000 characters.")]
        public string Description { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }

        [StringLength(50, ErrorMessage = "Size cannot be longer than 50 characters.")]
        public string? Size { get; set; }

        [StringLength(50, ErrorMessage = "Color cannot be longer than 50 characters.")]
        public string? Color { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal? PurchasePrice { get; set; }
        public int? PurchaseQuantity { get; set; }
        public int? RentalQuantity { get; set; }
        public decimal SecurityDeposit { get; set; }
        public string? RentalStatus { get; set; }
        public string? PurchaseStatus { get; set; }
        public string? Gender { get; set; }
        public string? AvailabilityStatus { get; set; }
    }
}
