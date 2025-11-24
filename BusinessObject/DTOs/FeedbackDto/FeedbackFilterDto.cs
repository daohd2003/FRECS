namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackFilterDto
    {
        public string? SearchTerm { get; set; }
        public int? Rating { get; set; } // null = All Ratings, 1-5 = specific rating
        public string? ResponseStatus { get; set; } // "All", "Responded", "NoResponse"
        public string? TimeFilter { get; set; } // "AllTime", "Today", "ThisWeek", "ThisMonth"
        public bool? IsBlocked { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } // "CreatedAt", "Rating"
        public string? SortOrder { get; set; } // "asc", "desc"
    }
}
