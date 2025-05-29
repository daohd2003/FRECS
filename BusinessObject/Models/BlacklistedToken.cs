using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    [Table("BlacklistedTokens")]
    public class BlacklistedToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime ExpiredAt { get; set; }
    }
}
