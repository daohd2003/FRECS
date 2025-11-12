using System;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.Models
{
    public class SystemConfig
    {
        [Key]
        [Required]
        [MaxLength(100)]
        public string Key { get; set; }

        [Required]
        [MaxLength(500)]
        public string Value { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Guid? UpdatedByAdminId { get; set; }
    }
}
