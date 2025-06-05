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
        public Guid UserId { get; set; }
        [MinLength(6)]
        [SpecialCharacter]
        public string CurrentPassword { get; set; }
        [MinLength(6)]
        [SpecialCharacter]
        public string NewPassword { get; set; }
    }
}
