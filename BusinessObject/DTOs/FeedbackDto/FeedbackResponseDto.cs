using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackResponseDto
    {
        public Guid FeedbackId { get; set; }
        public FeedbackTargetType TargetType { get; set; }
        public Guid TargetId { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }
        public string? OrderIdFromFeedback { get; set; } // ID của Order mà feedback thuộc về
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? ProviderResponse { get; set; }
        public DateTime? ProviderResponseAt { get; set; }
        public Guid? ProviderResponseById { get; set; }
        public string? ProviderResponderName { get; set; }
        
        // Added for frontend display
        public string? ProfilePictureUrl { get; set; }
        
        // Moderation fields
        public bool IsBlocked { get; set; }
        public bool IsVisible { get; set; }
        public string? ViolationReason { get; set; }
    }
}
