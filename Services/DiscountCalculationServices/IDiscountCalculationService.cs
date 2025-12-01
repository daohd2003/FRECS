using BusinessObject.DTOs.Discount;

namespace Services.DiscountCalculationServices
{
    public interface IDiscountCalculationService
    {
        /// <summary>
        /// Calculate automatic discounts for rental orders (legacy method)
        /// - Loyalty discount: 2% per previous rental × item count, max 15%
        /// </summary>
        Task<AutoDiscountResultDto> CalculateAutoDiscountsAsync(Guid customerId, int rentalDays, int itemCount, decimal subtotal);

        /// <summary>
        /// Calculate automatic discounts with specific product IDs
        /// - Item Rental Discount: Based on product's total RentCount (1% per 3 times rented, max 30%)
        /// - Loyalty Discount: Based on total completed rentals on platform, fixed amount (not affected by quantity or days)
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="productIds">List of product IDs being rented</param>
        /// <param name="baseDailyTotal">Total daily rate × quantity for all rental items</param>
        /// <param name="baseAmountForLoyalty">Fixed base amount for loyalty discount calculation</param>
        Task<AutoDiscountResultDto> CalculateAutoDiscountsWithItemsAsync(Guid customerId, List<Guid> productIds, decimal baseDailyTotal, decimal baseAmountForLoyalty);
    }
}
