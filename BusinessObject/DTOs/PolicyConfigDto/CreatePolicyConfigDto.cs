using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.PolicyConfigDto
{
    public class CreatePolicyConfigDto
    {
        [Required(ErrorMessage = "Policy name is required")]
        [MaxLength(200, ErrorMessage = "Policy name cannot exceed 200 characters")]
        public string PolicyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
