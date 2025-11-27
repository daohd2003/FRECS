using BusinessObject.DTOs.Discount;
using BusinessObject.Enums;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.DiscountCalculationServices
{
    public class DiscountCalculationService : IDiscountCalculationService
    {
        private readonly ShareItDbContext _context;

        // Discount constants
        private const decimal RENTAL_DAYS_DISCOUNT_PERCENT_PER_DAY = 3m; // 3% per day per item
        private const decimal MAX_RENTAL_DAYS_DISCOUNT_PERCENT = 25m;    // Max 25%
        private const decimal LOYALTY_DISCOUNT_PERCENT_PER_RENTAL = 2m;  // 2% per previous rental per item
        private const decimal MAX_LOYALTY_DISCOUNT_PERCENT = 15m;        // Max 15%

        public DiscountCalculationService(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<AutoDiscountResultDto> CalculateAutoDiscountsAsync(
            Guid customerId, 
            int rentalDays, 
            int itemCount, 
            decimal subtotal)
        {
            var result = new AutoDiscountResultDto();

            // 1. Calculate rental days discount: 3% per day × item count, max 25%
            // Example: 3 days × 2 items = 3% × 3 × 2 = 18%
            decimal rentalDaysDiscountPercent = rentalDays * RENTAL_DAYS_DISCOUNT_PERCENT_PER_DAY * itemCount;
            rentalDaysDiscountPercent = Math.Min(rentalDaysDiscountPercent, MAX_RENTAL_DAYS_DISCOUNT_PERCENT);
            
            result.RentalDaysDiscountPercent = rentalDaysDiscountPercent;
            result.RentalDaysDiscountAmount = Math.Round(subtotal * (rentalDaysDiscountPercent / 100), 0);

            // 2. Calculate loyalty discount: 2% per previous completed rental × item count, max 15%
            // Example: 3 previous rentals × 2 items = 2% × 3 × 2 = 12%
            var previousRentalCount = await _context.Orders
                .Where(o => o.CustomerId == customerId 
                         && o.Status == OrderStatus.returned
                         && o.Items.Any(i => i.TransactionType == TransactionType.rental))
                .CountAsync();

            result.PreviousRentalCount = previousRentalCount;

            decimal loyaltyDiscountPercent = previousRentalCount * LOYALTY_DISCOUNT_PERCENT_PER_RENTAL * itemCount;
            loyaltyDiscountPercent = Math.Min(loyaltyDiscountPercent, MAX_LOYALTY_DISCOUNT_PERCENT);

            result.LoyaltyDiscountPercent = loyaltyDiscountPercent;
            result.LoyaltyDiscountAmount = Math.Round(subtotal * (loyaltyDiscountPercent / 100), 0);

            // 3. Calculate total auto discount
            result.TotalAutoDiscount = result.RentalDaysDiscountAmount + result.LoyaltyDiscountAmount;

            return result;
        }
    }
}
