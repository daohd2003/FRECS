namespace BusinessObject.DTOs.Discount
{
    public class AutoDiscountResultDto
    {
        /// <summary>
        /// Item rental count discount percentage (1% per 3 times product has been rented, max 30%)
        /// </summary>
        public decimal ItemRentalCountDiscountPercent { get; set; }

        /// <summary>
        /// Item rental count discount amount
        /// </summary>
        public decimal ItemRentalCountDiscountAmount { get; set; }

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

        /// <summary>
        /// Total RentCount of products in cart (how many times products have been rented)
        /// </summary>
        public int TotalItemRentalCount { get; set; }
    }
}
