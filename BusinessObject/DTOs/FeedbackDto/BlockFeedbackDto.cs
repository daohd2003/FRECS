namespace BusinessObject.DTOs.FeedbackDto
{
    public class BlockFeedbackDto
    {
        public Guid FeedbackId { get; set; }
        public Guid BlockedById { get; set; }
        public string? Reason { get; set; }
    }
    
    public class UnblockFeedbackDto
    {
        public Guid FeedbackId { get; set; }
    }
}
