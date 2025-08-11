using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.Login
{
    public class FacebookLoginRequestDto
    {
        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }
}


