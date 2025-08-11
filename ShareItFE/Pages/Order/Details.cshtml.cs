using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.OrdersDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShareItFE.Common.Utilities;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ShareItFE.Pages.Order
{
    public class DetailsModel : PageModel
    {
        private readonly AuthenticatedHttpClientHelper _clientHelper;

        public DetailsModel(AuthenticatedHttpClientHelper clientHelper)
        {
            _clientHelper = clientHelper;
        }

        [BindProperty]
        public OrderDetailsDto Order { get; set; }
        
        // Pricing information for each order item
        public Dictionary<Guid, OrderItemPricingInfo> ItemPricingInfo { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            try
            {
                // 1. Lấy HttpClient đã được xác thực
                var client = await _clientHelper.GetAuthenticatedClientAsync();

                // 2. Gọi đến endpoint mới trên API
                var response = await client.GetAsync($"api/orders/{id}/details");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDetailsDto>>();
                    if (apiResponse != null)
                    {
                        Order = apiResponse.Data;
                        if (Order == null)
                        {
                            return NotFound();
                        }
                        
                        // Load pricing information for each order item
                        await LoadOrderItemsPricingInfo(client);
                        
                        return Page();
                    }
                    else
                    {
                        // Xử lý trường hợp API trả về success=false
                        TempData["ErrorMessage"] = apiResponse?.Message ?? "Failed to load order details.";
                        return RedirectToPage("/Profile", new { tab = "orders" });
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    // Xử lý các lỗi HTTP khác
                    TempData["ErrorMessage"] = $"Error loading order: {response.ReasonPhrase}";
                    return RedirectToPage("/Profile", new { tab = "orders" });
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi kết nối hoặc lỗi ngoài lệ khác
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return RedirectToPage("/Profile", new { tab = "orders" });
            }
        }

        private async Task LoadOrderItemsPricingInfo(HttpClient client)
        {
            if (Order?.Items == null) return;

            // Get discount rate and max discount times from API once for all items
            decimal discountRate = 8m; // Default fallback
            int maxDiscountTimes = 5; // Default fallback
            try
            {
                var settingsResponse = await client.GetAsync("api/pricing/settings");
                if (settingsResponse.IsSuccessStatusCode)
                {
                    var settingsApiResponse = await settingsResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
                    if (settingsApiResponse?.Data != null)
                    {
                        var settingsData = JsonSerializer.Deserialize<JsonElement>(settingsApiResponse.Data.ToString());
                        discountRate = settingsData.GetProperty("discountRate").GetDecimal();
                        maxDiscountTimes = settingsData.GetProperty("maxDiscountTimes").GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading pricing settings: {ex.Message}");
                // Use default values
            }

            foreach (var item in Order.Items)
            {
                try
                {
                    var pricingResponse = await client.GetAsync($"api/pricing/product/{item.ProductId}");
                    if (pricingResponse.IsSuccessStatusCode)
                    {
                        var pricingApiResponse = await pricingResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
                        if (pricingApiResponse?.Data != null)
                        {
                            var pricingData = JsonSerializer.Deserialize<JsonElement>(pricingApiResponse.Data.ToString());
                            
                            var discountPercentage = pricingData.GetProperty("discountPercentage").GetDecimal();
                            
                            var rentCount = pricingData.GetProperty("rentCount").GetInt32();
                            var isMaxDiscount = rentCount >= maxDiscountTimes;
                            
                            ItemPricingInfo[item.ProductId] = new OrderItemPricingInfo
                            {
                                OriginalPrice = pricingData.GetProperty("originalPrice").GetDecimal(),
                                CurrentPrice = pricingData.GetProperty("currentPrice").GetDecimal(),
                                DiscountPercentage = discountPercentage,
                                IsDiscounted = pricingData.GetProperty("isDiscounted").GetBoolean(),
                                RentCount = rentCount,
                                RentalsNeededForDiscount = isMaxDiscount ? 0 : CalculateRentalsNeeded(discountPercentage, discountRate),
                                IsMaxDiscount = isMaxDiscount
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading pricing for product {item.ProductId}: {ex.Message}");
                    // Set default values if pricing fetch fails
                    ItemPricingInfo[item.ProductId] = new OrderItemPricingInfo
                    {
                        OriginalPrice = item.PricePerDay,
                        CurrentPrice = item.PricePerDay,
                        DiscountPercentage = 0,
                        IsDiscounted = false,
                        RentCount = 0,
                        RentalsNeededForDiscount = 0,
                        IsMaxDiscount = false
                    };
                }
            }
        }

        private int CalculateRentalsNeeded(decimal discountPercentage, decimal discountRate)
        {
            if (discountPercentage <= 0 || discountRate <= 0) return 0;
            
            return (int)Math.Ceiling(discountPercentage / discountRate);
        }
    }

    public class OrderItemPricingInfo
    {
        public decimal OriginalPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsDiscounted { get; set; }
        public int RentCount { get; set; }
        public int RentalsNeededForDiscount { get; set; }
        public bool IsMaxDiscount { get; set; }
    }
}