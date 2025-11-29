using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.FeedbackDto
{
    /// <summary>
    /// DTO for updating provider response to feedback
    /// </summary>
    public class UpdateProviderResponseDto
    {
        [Required(ErrorMessage = "Response content is required.")]
        [StringLength(1000, ErrorMessage = "Response content cannot exceed 1000 characters.")]
        [MinLength(1, ErrorMessage = "Response content cannot be empty.")]
        public string ResponseContent { get; set; } = string.Empty;
    }
}
