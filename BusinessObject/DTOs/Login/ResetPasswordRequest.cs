using BusinessObject.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.Login
{
    public class ResetPasswordRequest
    {
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        [MaxLength(128)]
        public string Email { get; set; }
        public string Token { get; set; }
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [Uppercase]
        [Lowercase]
        [Numeric]
        [SpecialCharacter]
        public string NewPassword { get; set; }
    }
}
