using BusinessObject.DTOs.AIDtos;
using DataAccess;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using BusinessObject.Enums;

namespace Services.AI
{
    public class AiSearchService : IAiSearchService
    {
        private readonly ShareItDbContext _context;
        private readonly OpenAIOptions _openAIOptions;
        private readonly ILogger<AiSearchService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _baseAppUrl;
        private readonly IMemoryCache _cache;

        public AiSearchService(
            ShareItDbContext context,
            IOptions<OpenAIOptions> openAIOptions,
            ILogger<AiSearchService> logger,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _openAIOptions = openAIOptions.Value;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
            _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            _baseAppUrl = _openAIOptions.BaseAppUrl;
            _cache = cache;
        }

        public async Task<string> AskAboutFRECSAsync(string question, Guid? userId = null)
        {
            _logger.LogInformation("Received question: {Question} from user: {UserId}", question, userId?.ToString() ?? "anonymous");

            var products = await GetCachedProductsAsync();
            var contextString = BuildProductContext(products);
            
            // Get user history if logged in
            string? userHistoryContext = null;
            if (userId.HasValue)
            {
                userHistoryContext = await BuildUserHistoryContext(userId.Value);
            }

            var prompt = BuildPrompt(contextString, question, products.Any(), userHistoryContext);

            var responseText = await SendRequestToGeminiAsync(prompt);

            return responseText ?? "No response.";
        }

        private async Task<List<dynamic>> GetCachedProductsAsync()
        {
            const string cacheKey = "FRECS_Products";
            if (_cache.TryGetValue(cacheKey, out List<dynamic> cachedProducts))
            {
                return cachedProducts;
            }

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.AvailabilityStatus == AvailabilityStatus.available)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Size,
                    p.PricePerDay,
                    p.PurchasePrice,
                    p.RentalQuantity,
                    p.PurchaseQuantity,
                    Category = p.Category.Name,
                    p.Color
                })
                .Take(50)
                .ToListAsync();

            _cache.Set(cacheKey, products, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

            return products.Cast<dynamic>().ToList();
        }

        private string BuildProductContext(List<dynamic> products)
        {
            if (products == null || products.Count == 0)
            {
                return "The store currently has no clothing items available for rent or purchase.";
            }

            var lines = products.Select(p =>
            {
                string link = $"{_baseAppUrl}/products/detail/{p.Id}";
                
                // Build pricing information
                var pricingInfo = new List<string>();
                if (p.RentalQuantity > 0 && p.PricePerDay > 0)
                {
                    pricingInfo.Add($"Rental: {((decimal)p.PricePerDay).ToString("N0")} VND/day");
                }
                if (p.PurchaseQuantity > 0 && p.PurchasePrice > 0)
                {
                    pricingInfo.Add($"Purchase: {((decimal)p.PurchasePrice).ToString("N0")} VND");
                }
                
                var priceString = pricingInfo.Any() ? string.Join(" | ", pricingInfo) : "Contact for pricing";
                
                return $"- {p.Name} | Size: {p.Size} | Category: {p.Category} | Color: {p.Color}\n  {priceString}\n  [Xem chi tiết]({link})";
            });

            return string.Join("\n", lines);
        }

        private async Task<string?> BuildUserHistoryContext(Guid userId)
        {
            try
            {
                var recentOrders = await _context.Orders
                    .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Category)
                    .Where(o => o.CustomerId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5) // Last 5 orders
                    .ToListAsync();

                if (!recentOrders.Any())
                    return null;

                var historyLines = new List<string>();
                foreach (var order in recentOrders)
                {
                    foreach (var item in order.Items)
                    {
                        var transactionType = item.TransactionType == TransactionType.rental ? "Rented" : "Purchased";
                        var additionalInfo = item.TransactionType == TransactionType.rental && item.RentalDays.HasValue
                            ? $" (for {item.RentalDays} days)"
                            : "";
                        
                        historyLines.Add($"- Previously {transactionType}: {item.Product.Name} | Category: {item.Product.Category.Name} | Color: {item.Product.Color} | Size: {item.Product.Size}{additionalInfo}");
                    }
                }

                return string.Join("\n", historyLines.Distinct().Take(10)); // Max 10 unique items
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build user history context for user {UserId}", userId);
                return null;
            }
        }

        private string BuildPrompt(string context, string question, bool hasProducts, string? userHistoryContext = null)
        {
            if (!hasProducts)
            {
                return $"You are an assistant for the FRECS clothing rental & sales store. No product information is available currently.\n\nUser's question: {question}\n\nPlease politely inform the user in English that product details are currently unavailable. Suggest visiting the store website at {_baseAppUrl} to check for available products.";
            }

            var historySection = string.IsNullOrEmpty(userHistoryContext)
                ? ""
                : $@"

User's Purchase/Rental History:
{userHistoryContext}

Use this history to provide personalized recommendations when relevant.";

            return $@"You are a helpful assistant for the FRECS clothing rental & sales store. Only respond using the provided product list.

Products (each product already has a clickable detail link):
{context}{historySection}

User's question: {question}

CRITICAL INSTRUCTIONS - MUST FOLLOW:
1. **ALWAYS include the [View Details] link** for EVERY product you mention, regardless of how the user phrases their question.
2. Even for simple questions like:
   - 'Show me black clothes' 
   - 'What products are blue'
   - 'Do you have any shirts'
   You MUST include the detail link provided in the product list.
3. Copy the exact [Xem chi tiết](link) from the product information above - don't create new links, but replace the text with 'View Details'.
4. Format your response to include product details AND the link on a new line.
5. Products may have RENTAL price, PURCHASE price, or BOTH. Always show the pricing information clearly:
   - Rental price format: number VND/day for rental options
   - Purchase price format: number VND for purchase options
   - Show both if available
6. If no matching products are found, politely inform the user in English.
7. Keep responses concise, helpful, and friendly.
8. If user history is provided, use it to offer personalized suggestions (e.g., 'Based on your previous rentals, you might also like...').

Example response format:
Here are the matching products:
- White Shirt | Size: M | Category: Shirt | Color: White
  Rental: 50,000 VND/day | Purchase: 500,000 VND
  [View Details](link)

Answer in English:";
        }

        private async Task<string?> SendRequestToGeminiAsync(string prompt)
        {
            var apiKey = _openAIOptions.ApiKey;
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

            var requestData = new
            {
                contents = new[]
                {
            new
            {
                parts = new[]
                {
                    new { text = prompt }
                }
            }
        }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.PostAsync(url, content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    return "Sorry, the assistant cannot answer the question at this time.";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling Gemini API");
                return "An error occurred while contacting the assistant service.";
            }
        }
    }
}