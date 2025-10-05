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

        public async Task<string> AskAboutFRECSAsync(string question)
        {
            _logger.LogInformation("Received question: {Question}", question);

            var products = await GetCachedProductsAsync();
            var contextString = BuildProductContext(products);
            var prompt = BuildPrompt(contextString, question, products.Any());

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
                    p.Description,
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
                return $"- {p.Name} | Size: {p.Size} | Category: {p.Category} | Color: {p.Color} | Price: {p.PricePerDay} VND\n  Description: {p.Description}\n  [Xem chi tiết]({link})";
            });

            return string.Join("\n", lines);
        }

        private string BuildPrompt(string context, string question, bool hasProducts)
        {
            if (!hasProducts)
            {
                return $"You are an assistant for the FRECS clothing rental & sales store. No product information is available currently.\n\nUser's question: {question}\n\nPlease politely inform the user in English that product details are currently unavailable. Suggest visiting the store website at {_baseAppUrl} to check for available products.";
            }

            return $@"You are a helpful assistant for the FRECS clothing rental & sales store. Only respond using the provided product list.

Products (each product already has a clickable detail link):
{context}

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
5. If no matching products are found, politely inform the user in English.
6. Keep responses concise, helpful, and friendly.

Example response format:
Here are the matching products:
- White Shirt | Size: M | Category: Shirt | Color: White | Price: 50,000 VND
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