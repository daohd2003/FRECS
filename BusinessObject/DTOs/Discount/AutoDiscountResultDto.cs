namespace BusinessObject.DTOs.Discount
{
    public class AutoDiscountResultDto
    {
        /// <summary>
        /// Rental days discount percentage (3% per day × item count, max 25%)
        /// </summary>
        public decimal RentalDaysDiscountPercent { get; set; }

        /// <summary>
        /// Rental days discount amount
        /// </summary>
        public decimal RentalDaysDiscountAmount { get; set; }

        /// <summary>
        /// Loyalty discount percentage (2% per previous rental × item count, max 15%)
        /// </summary>
        public decimal LoyaltyDiscountPercent { get; set; }

        /// <summary>
        /// Loyalty discount amount
        /// </summary>
        public decimal LoyaltyDiscountAmount { get; set; }

        /// <summary>
        /// Total automatic discount amount
        /// </summary>
        public decimal TotalAutoDiscount { get; set; }

        /// <summary>
        /// Number of previous completed rentals
        /// </summary>
        public int PreviousRentalCount { get; set; }
    }
}
