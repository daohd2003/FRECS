namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackStatisticsDto
    {
        public int TotalFeedbacks { get; set; }
        public double AverageRating { get; set; }
        public int FlaggedContent { get; set; }
        public int RespondedCount { get; set; }
        public int BlockedCount { get; set; }
    }
}
