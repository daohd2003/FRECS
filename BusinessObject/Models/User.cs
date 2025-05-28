using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    public class User
    {

        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(128)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Role { get; set; } = UserRole.Customer.ToString();

        public string AvatarUrl { get; set; } = string.Empty;

        [MaxLength(200)]
        public string RefreshToken { get; set; }

        public DateTime RefreshTokenExpiryTime { get; set; }

        public string GoogleId { get; set; } = string.Empty;
    }
}
