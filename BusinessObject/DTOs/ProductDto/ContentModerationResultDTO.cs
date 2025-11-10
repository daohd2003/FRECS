namespace BusinessObject.DTOs.ProductDto
{
    public class ContentModerationResultDTO
    {
        public bool IsAppropriate { get; set; }
        public string? Reason { get; set; }
        public List<string>? ViolatedTerms { get; set; }
    }
}

