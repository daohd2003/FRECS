using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.FeedbackDto
{
    public class FeedbackRequestDto
    {
        [Required(ErrorMessage = "Target Type is required.")]
        public FeedbackTargetType TargetType { get; set; }

        [Required(ErrorMessage = "Target ID (Product or Order) is required.")]
        public Guid TargetId { get; set; } // ProductId hoặc OrderId

        // OrderItemId chỉ được cung cấp nếu TargetType là Product, nếu không sẽ là null
        public Guid? OrderItemId { get; set; }

        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
        public string? Comment { get; set; }
    }
}
