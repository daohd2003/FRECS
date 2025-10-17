using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Models
{
    /// <summary>
    /// Đại diện cho sản phẩm cho thuê
    /// </summary>
    public class Product
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ProviderId { get; set; }

        [ForeignKey(nameof(ProviderId))]
        public User Provider { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category Category { get; set; }

        [MaxLength(50)]
        public string Size { get; set; }

        [MaxLength(50)]
        public string Color { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerDay { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PurchasePrice { get; set; } = 0m;

        public int PurchaseQuantity { get; set; } = 0;

        public int RentalQuantity { get; set; } = 0;

        [Required]
        public AvailabilityStatus AvailabilityStatus { get; set; }

        /// <summary>
        /// Sản phẩm có được quảng cáo hay không
        /// </summary>
        public bool IsPromoted { get; set; } = false;

        public int RentCount { get; set; } = 0;

        public int BuyCount { get; set; } = 0;

        [Required]
        public RentalStatus RentalStatus { get; set; }

        [Required]
        public PurchaseStatus PurchaseStatus { get; set; }

        [Required]
        public Gender Gender { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        [Column(TypeName = "decimal(2,1)")]
        public decimal AverageRating { get; set; } = 0.0m;
        public int RatingCount { get; set; } = 0;

        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

        [Column(TypeName = "decimal(10,2)")]
        public decimal SecurityDeposit { get; set; } = 0m;
    }
}
