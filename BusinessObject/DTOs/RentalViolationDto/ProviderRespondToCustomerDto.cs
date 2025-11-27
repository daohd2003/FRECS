using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO để Provider phản hồi Customer's rejection
    /// </summary>
    public class ProviderRespondToCustomerDto
    {
        /// <summary>
        /// Phản hồi của Provider đối với lý do từ chối của Customer
        /// </summary>
        [Required(ErrorMessage = "Response is required")]
        [StringLength(2000, ErrorMessage = "Response cannot exceed 2000 characters")]
        public string Response { get; set; } = string.Empty;
    }
}
