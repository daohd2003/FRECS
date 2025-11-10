using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ProfileDtos
{
    public class ProfileUpdateDto
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [MaxLength(255)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [MaxLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
        public string Phone { get; set; } = String.Empty;

        public string Address { get; set; } = String.Empty;
    }
}
