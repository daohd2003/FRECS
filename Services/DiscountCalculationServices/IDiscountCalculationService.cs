using BusinessObject.DTOs.Discount;

namespace Services.DiscountCalculationServices
{
    public interface IDiscountCalculationService
    {
        /// <summary>
        /// Calculate automatic discounts for rental orders
        /// - Rental days discount: 3% per day × item count, max 25%
        /// - Loyalty discount: 2% per previous rental × item count, max 15%
        /// </summary>
        Task<AutoDiscountResultDto> CalculateAutoDiscountsAsync(Guid customerId, int rentalDays, int itemCount, decimal subtotal);
    }
}
