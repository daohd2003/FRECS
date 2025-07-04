using System.Text.Json.Serialization;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackDto
    {
        [JsonPropertyName("feedbackId")]
        public Guid Id { get; set; }

        [JsonPropertyName("targetId")]
        public Guid TargetId { get; set; }

        [JsonPropertyName("customerName")]
        public string UserName { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("submittedAt")]
        public DateTime CreatedAt { get; set; }

        // Bạn có thể thêm các trường khác nếu cần
        // [JsonPropertyName("providerResponse")]
        // public string ProviderResponse { get; set; }
    }
}
