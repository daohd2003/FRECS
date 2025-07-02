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

        public async Task<string> AskAboutShareITAsync(string question)
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
            const string cacheKey = "ShareIT_Products";
            if (_cache.TryGetValue(cacheKey, out List<dynamic> cachedProducts))
            {
                return cachedProducts;
            }

            var products = await _context.Products
                .Where(p => p.AvailabilityStatus == AvailabilityStatus.available)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Size,
                    p.PricePerDay,
                    p.Description,
                    p.Category,
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
                return "The store currently has no clothing items available for rent.";
            }

            var lines = products.Select(p =>
            {
                string link = $"{_baseAppUrl}/api/products/{p.Id}";
                return $"- {p.Name} | Size: {p.Size} | Category: {p.Category} | Color: {p.Color} | Price: {p.PricePerDay} VND\n  Description: {p.Description}\n  [Xem chi tiết]({link})";
            });

            return string.Join("\n", lines);
        }

        private string BuildPrompt(string context, string question, bool hasProducts)
        {
            if (!hasProducts)
            {
                return $"You are an assistant for the ShareIT clothing rental store. No product information is available currently.\n\nUser's question: {question}\n\nPlease inform the user that product details are currently unavailable. Suggest visiting the store at {_baseAppUrl}.";
            }

            return $"You are a helpful assistant for the ShareIT clothing rental store. Only respond using the provided product list.\n\nProducts:\n{context}\n\nUser's question: {question}\n\nAnswer with matching items, their names, prices, and **direct links**. If not found, say so.";
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