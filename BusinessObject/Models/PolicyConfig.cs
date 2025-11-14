using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObject.Models
{
    [Table("PolicyConfigs")]
    public class PolicyConfig
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string PolicyName { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Guid? UpdatedByAdminId { get; set; }

        [ForeignKey(nameof(UpdatedByAdminId))]
        public virtual User? UpdatedByAdmin { get; set; }
    }
}
