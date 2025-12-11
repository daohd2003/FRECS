using System;

namespace BusinessObject.DTOs.ConversationDtos
{
    /// <summary>
    /// DTO for product picker in chat - lightweight version for quick loading
    /// </summary>
    public class ChatProductPickerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? ProviderName { get; set; }
        public string? Category { get; set; }
    }

    /// <summary>
    /// Request DTO for getting products for chat picker
    /// </summary>
    public class ChatProductPickerRequestDto
    {
        public string? SearchTerm { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
