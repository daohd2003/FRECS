using BusinessObject.Enums;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackManagementDto
    {
        public Guid FeedbackId { get; set; }
        
        // Product Info
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImageUrl { get; set; }
        public decimal? ProductPrice { get; set; }
        public string? ProviderName { get; set; }
        
        // Customer Info
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerProfilePicture { get; set; }
        
        // Feedback Info
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Provider Response
        public string? ProviderResponse { get; set; }
        public DateTime? ProviderResponseAt { get; set; }
        public string? ProviderResponderName { get; set; }
        
        // Status
        public bool IsBlocked { get; set; }
        public bool IsVisible { get; set; }
        public DateTime? BlockedAt { get; set; }
        public string? BlockedByName { get; set; }
        
        // Computed
        public string Status { get; set; } // "Responded", "Flagged", "Blocked", "Pending"
    }
}
