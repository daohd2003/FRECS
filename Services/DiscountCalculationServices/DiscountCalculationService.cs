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
        private const decimal ITEM_RENTAL_COUNT_DISCOUNT_PERCENT_PER_3_RENTALS = 1m; // 1% per 3 times product has been rented
        private const decimal MAX_ITEM_RENTAL_COUNT_DISCOUNT_PERCENT = 30m;          // Max 30%
        private const decimal LOYALTY_DISCOUNT_PERCENT_PER_RENTAL = 2m;           // 2% per previous rental per item
        private const decimal MAX_LOYALTY_DISCOUNT_PERCENT = 15m;                 // Max 15%

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

            // Note: This method is called from CartController without specific item IDs
            // For accurate item rental count discount, use CalculateAutoDiscountsWithItemsAsync instead
            // Here we just set 0 for item rental count discount
            result.ItemRentalCountDiscountPercent = 0;
            result.ItemRentalCountDiscountAmount = 0;
            result.TotalItemRentalCount = 0;

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
            result.TotalAutoDiscount = result.ItemRentalCountDiscountAmount + result.LoyaltyDiscountAmount;

            return result;
        }

        /// <summary>
        /// Calculate auto discounts with specific product IDs
        /// - Item Rental Discount: Based on product's total RentCount (1% per 3 times rented, max 30%)
        /// - Loyalty Discount: Based on total completed rentals on platform, fixed amount (not affected by quantity or days)
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="productIds">List of product IDs being rented</param>
        /// <param name="baseDailyTotal">Total daily rate × quantity for all rental items (giá/ngày × số lượng, không nhân số ngày)</param>
        /// <param name="baseAmountForLoyalty">Fixed base amount for loyalty discount calculation (e.g., average daily rate)</param>
        public async Task<AutoDiscountResultDto> CalculateAutoDiscountsWithItemsAsync(
            Guid customerId,
            List<Guid> productIds,
            decimal baseDailyTotal,
            decimal baseAmountForLoyalty)
        {
            var result = new AutoDiscountResultDto();

            // 1. Calculate item rental count discount: 1% per 3 times product has been rented (RentCount), max 30%
            // Get total RentCount from all products in cart
            // Discount is applied to baseDailyTotal (giá/ngày × số lượng) - FIXED regardless of rental days
            int totalProductRentCount = 0;
            foreach (var productId in productIds)
            {
                var product = await _context.Products
                    .Where(p => p.Id == productId)
                    .Select(p => new { p.RentCount })
                    .FirstOrDefaultAsync();
                
                if (product != null)
                {
                    totalProductRentCount += product.RentCount;
                }
            }

            result.TotalItemRentalCount = totalProductRentCount;

            // 1% discount per 3 rented times, max 30%
            decimal itemRentalCountDiscountPercent = Math.Floor(totalProductRentCount / 3m) * ITEM_RENTAL_COUNT_DISCOUNT_PERCENT_PER_3_RENTALS;
            itemRentalCountDiscountPercent = Math.Min(itemRentalCountDiscountPercent, MAX_ITEM_RENTAL_COUNT_DISCOUNT_PERCENT);

            result.ItemRentalCountDiscountPercent = itemRentalCountDiscountPercent;
            // Discount applied to baseDailyTotal (giá/ngày × số lượng) - FIXED regardless of rental days
            // Increases only when quantity increases
            result.ItemRentalCountDiscountAmount = Math.Round(baseDailyTotal * (itemRentalCountDiscountPercent / 100), 0);

            // 2. Calculate loyalty discount: 2% per previous completed rental, max 15%
            // FIXED amount - does NOT change with quantity or rental days
            // Only increases when customer completes more rentals on platform
            var previousRentalCount = await _context.Orders
                .Where(o => o.CustomerId == customerId
                         && o.Status == OrderStatus.returned
                         && o.Items.Any(i => i.TransactionType == TransactionType.rental))
                .CountAsync();

            result.PreviousRentalCount = previousRentalCount;

            // Loyalty discount % is based only on previous rental count (NOT multiplied by item count)
            decimal loyaltyDiscountPercent = previousRentalCount * LOYALTY_DISCOUNT_PERCENT_PER_RENTAL;
            loyaltyDiscountPercent = Math.Min(loyaltyDiscountPercent, MAX_LOYALTY_DISCOUNT_PERCENT);

            result.LoyaltyDiscountPercent = loyaltyDiscountPercent;
            // Loyalty discount is FIXED - applied to baseAmountForLoyalty (not affected by quantity or days)
            result.LoyaltyDiscountAmount = Math.Round(baseAmountForLoyalty * (loyaltyDiscountPercent / 100), 0);

            // 3. Calculate total auto discount
            result.TotalAutoDiscount = result.ItemRentalCountDiscountAmount + result.LoyaltyDiscountAmount;

            return result;
        }
    }
}
