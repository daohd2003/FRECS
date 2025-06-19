using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class SubmitProviderResponseDto
    {
        [Required(ErrorMessage = "Response content is required.")]
        [MinLength(10, ErrorMessage = "Response must be at least 10 characters long.")]
        [MaxLength(1000, ErrorMessage = "Response cannot exceed 1000 characters.")]
        public string ResponseContent { get; set; }
    }
}
