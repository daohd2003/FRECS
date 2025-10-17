using BusinessObject.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.Login
{
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Current password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [Uppercase]
        [Lowercase]
        [Numeric]
        [SpecialCharacter]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [Uppercase]
        [Lowercase]
        [Numeric]
        [SpecialCharacter]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirmation password is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
