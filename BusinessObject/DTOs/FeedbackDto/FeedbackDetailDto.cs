using BusinessObject.Enums;
using System.Text.Json.Serialization;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackDetailDto
    {
        public Guid FeedbackId { get; set; }
        
        // Product Information
        [JsonPropertyName("product")]
        public ProductInfoDto? Product { get; set; }
        
        // Order Item Information (if feedback is for a product from an order)
        [JsonPropertyName("orderItem")]
        public OrderItemInfoDto? OrderItem { get; set; }
        
        // Customer Information (nested)
        [JsonPropertyName("customer")]
        public CustomerInfoDto Customer { get; set; }
        
        // Customer Information (flat - for compatibility)
        [JsonPropertyName("customerId")]
        public Guid CustomerId { get; set; }
        
        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }
        
        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; }
        
        [JsonPropertyName("customerProfilePicture")]
        public string? CustomerProfilePicture { get; set; }
        
        // Feedback Content
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Provider Response
        [JsonPropertyName("providerResponse")]
        public ProviderResponseInfoDto? ProviderResponse { get; set; }
        
        // Status Information
        [JsonPropertyName("status")]
        public StatusInfoDto Status { get; set; }
    }
    
    public class ProductInfoDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string? Description { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal PurchasePrice { get; set; }
        public string RentalStatus { get; set; }
        public string PurchaseStatus { get; set; }
        public int RentalQuantity { get; set; }
        public int PurchaseQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public string ProviderName { get; set; }
        public string? ProviderEmail { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
    
    public class CustomerInfoDto
    {
        [JsonPropertyName("customerId")]
        public Guid CustomerId { get; set; }
        
        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; }
        
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        
        [JsonPropertyName("profilePicture")]
        public string? ProfilePicture { get; set; }
        
        [JsonPropertyName("submittedAt")]
        public DateTime SubmittedAt { get; set; }
    }
    
    public class ProviderResponseInfoDto
    {
        public string ResponseText { get; set; }
        public string ResponderName { get; set; }
        public DateTime RespondedAt { get; set; }
    }
    
    public class OrderItemInfoDto
    {
        public Guid OrderItemId { get; set; }
        public Guid OrderId { get; set; }
        public string TransactionType { get; set; } // "Rental" or "Purchase"
        public int Quantity { get; set; }
        public int? RentalDays { get; set; }
        public decimal DailyRate { get; set; }
        public decimal DepositPerUnit { get; set; }
        public decimal TotalPrice { get; set; } // Calculated: DailyRate * RentalDays * Quantity (for rental) or DailyRate * Quantity (for purchase)
    }
    
    public class StatusInfoDto
    {
        public string Visibility { get; set; } // "Visible to public", "Hidden from public"
        public string ContentStatus { get; set; } // "Clear content", "Blocked content"
        public string ResponseStatus { get; set; } // "Responded", "No response"
        public bool IsBlocked { get; set; }
        public DateTime? BlockedAt { get; set; }
        public string? BlockedByName { get; set; }
    }
}
