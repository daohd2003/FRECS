using BusinessObject.Enums;

namespace BusinessObject.DTOs.UsersDto
{
    public class UserODataDTO
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.customer;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool? IsActive { get; set; }
        
        public string? ProfilePictureUrl { get; set; }
    }
}
