using BusinessObject.DTOs.AIDtos;
using BusinessObject.DTOs.ProductDto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Services.ContentModeration
{
    public class ContentModerationService : IContentModerationService
    {
        private readonly ContentModerationOptions _moderationOptions;
        private readonly ILogger<ContentModerationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public ContentModerationService(
            IOptions<ContentModerationOptions> moderationOptions,
            ILogger<ContentModerationService> logger,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _moderationOptions = moderationOptions.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("ContentModeration");
            _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            _cache = cache;
        }

        public async Task<ContentModerationResultDTO> CheckProductContentAsync(string name, string? description)
        {
            try
            {
                // Pre-validation check BEFORE calling AI (catches spam, repeating chars, etc.)
                Console.WriteLine($"[MODERATION DEBUG] Running pre-validation check...");
                var (isValid, reason, violatedTerms) = PerformPreValidation(name, description);

                if (!isValid)
                {
                    Console.WriteLine($"[MODERATION] Pre-validation FAILED: {reason}");
                    return new ContentModerationResultDTO
                    {
                        IsAppropriate = false,
                        Reason = reason,
                        ViolatedTerms = violatedTerms
                    };
                }

                // Check cache BEFORE calling AI API
                var cacheKey = GenerateCacheKey(name, description);
                if (_cache.TryGetValue(cacheKey, out ContentModerationResultDTO? cachedResult))
                {
                    Console.WriteLine($"[MODERATION CACHE] ✓ Cache HIT - Skipping API call");
                    return cachedResult!;
                }

                Console.WriteLine($"[MODERATION DEBUG] Pre-validation PASSED. Proceeding to AI check...");
                Console.WriteLine($"[MODERATION DEBUG] Input - Name: '{name}' | Description: '{description}'");

                var prompt = BuildModerationPrompt(name, description);
                Console.WriteLine($"[MODERATION DEBUG] Sending to AI...");

                // This will throw detailed exceptions if any error occurs
                var geminiResponse = await SendRequestToGeminiAsync(prompt);

                Console.WriteLine($"[MODERATION DEBUG] AI Response: {geminiResponse?.Substring(0, Math.Min(500, geminiResponse?.Length ?? 0))}...");

                if (string.IsNullOrEmpty(geminiResponse))
                {
                    Console.WriteLine($"[MODERATION DEBUG] Empty response from AI");
                    return new ContentModerationResultDTO
                    {
                        IsAppropriate = false,
                        Reason = "AI service returned an empty response. Please try again.",
                        ViolatedTerms = new List<string> { "empty_response" }
                    };
                }

                var result = ParseModerationResponse(geminiResponse);
                Console.WriteLine($"[MODERATION DEBUG] Final Decision - IsAppropriate: {result.IsAppropriate} | Reason: {result.Reason}");

                // Cache the result for 24 hours
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, result, cacheOptions);
                Console.WriteLine($"[MODERATION CACHE] ✓ Result cached for 24 hours");

                return result;
            }
            catch (InvalidOperationException ex)
            {
                return new ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = ex.Message,
                    ViolatedTerms = new List<string> { "ai_service_error" }
                };
            }
            catch (Exception ex)
            {
                return new ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = $"Unexpected error occurred: {ex.Message}. Please contact support if this persists.",
                    ViolatedTerms = new List<string> { "unexpected_error" }
                };
            }
        }

        private string GenerateCacheKey(string name, string? description)
        {
            // Create hash from content to use as cache key
            var content = $"{name?.Trim().ToLower()}|{description?.Trim().ToLower()}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            var hash = Convert.ToBase64String(hashBytes);
            return $"moderation:{hash}";
        }

        private (bool IsValid, string? Reason, List<string> ViolatedTerms) PerformPreValidation(string name, string? description)
        {
            var violatedTerms = new List<string>();

            // 1. Kiểm tra name không được rỗng hoặc toàn khoảng trắng
            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Product name cannot be empty", new List<string> { "empty name" });
            }

            // 2. Kiểm tra name phải có ít nhất 3 ký tự
            if (name.Trim().Length < 3)
            {
                return (false, "Product name must be at least 3 characters", new List<string> { "too short" });
            }

            // 3. Kiểm tra không được toàn số
            if (System.Text.RegularExpressions.Regex.IsMatch(name.Trim(), @"^\d+$"))
            {
                return (false, "Product name cannot contain only numbers", new List<string> { "numeric only" });
            }

            // 4. Kiểm tra không được toàn ký tự lặp (aaaa, 1111, etc.)
            if (IsRepeatingCharacters(name))
            {
                return (false, "Product name contains excessive repeating characters", new List<string> { "repeating characters" });
            }

            // 5. Kiểm tra phải có ít nhất 1 chữ cái
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"[a-zA-ZÀ-ỹ]"))
            {
                return (false, "Product name must contain at least one letter", new List<string> { "no letters" });
            }

            // 6. Kiểm tra description nếu có
            if (!string.IsNullOrWhiteSpace(description))
            {
                if (IsRepeatingCharacters(description))
                {
                    return (false, "Product description contains excessive repeating characters", new List<string> { "repeating characters" });
                }

                // Kiểm tra description không được quá ngắn nếu có
                if (description.Trim().Length < 10)
                {
                    return (false, "Product description must be at least 10 characters if provided", new List<string> { "description too short" });
                }
            }

            return (true, null, new List<string>());
        }

        private bool IsRepeatingCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Loại bỏ TẤT CẢ spaces và ký tự đặc biệt trước khi check
            var cleanText = new string(text.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLower();

            if (cleanText.Length < 4) return false;

            // ✅ FIX 1: Kiểm tra nếu có >70% ký tự giống nhau (bắt "111111", "aaaaaaa")
            var distinctChars = cleanText.Distinct().Count();
            var totalChars = cleanText.Length;

            // Nếu chỉ có 1-2 ký tự khác nhau trong chuỗi dài → spam
            if (totalChars >= 5 && distinctChars <= 2)
            {
                Console.WriteLine($"[SPAM DETECTED] Only {distinctChars} distinct chars in {totalChars} chars: '{text}'");
                return true;
            }

            // ✅ FIX 2: Kiểm tra pattern lặp: aaa, 111, xxx (GIẢM threshold xuống 3+)
            for (int i = 0; i < cleanText.Length - 2; i++)
            {
                if (cleanText[i] == cleanText[i + 1] && cleanText[i + 1] == cleanText[i + 2])
                {
                    // Nếu có 3+ ký tự liên tiếp giống nhau
                    int repeatCount = 3;
                    for (int j = i + 3; j < cleanText.Length && cleanText[j] == cleanText[i]; j++)
                    {
                        repeatCount++;
                    }

                    // ✅ GIẢM threshold: Nếu lặp >= 4 lần → spam (trước đây là >4)
                    if (repeatCount >= 4)
                    {
                        Console.WriteLine($"[SPAM DETECTED] Character '{cleanText[i]}' repeated {repeatCount} times in: '{text}'");
                        return true;
                    }
                }
            }

            return false;
        }

        private string BuildModerationPrompt(string name, string? description)
        {
            return $@"You are a STRICT content moderation system for an e-commerce clothing rental and sales platform.

CRITICAL: You MUST reject ANY product with inappropriate content. This is a family-friendly platform.

REJECT if product information contains ANY of these issues:

1. **Offensive language or profanity** - STRICTLY REJECT ANY content containing:
   - English profanity: fuck, fucking, shit, bitch, ass, damn, hell, dick, cock, pussy, cunt, bastard, asshole, motherfucker, etc.
   - Vietnamese profanity: địt, lồn, đụ, cặc, buồi, chó, súc vật, đéo, đệch, vãi, etc.
   - Any variations or attempts to bypass filters (f*ck, fck, fuk, etc.)
   - Profanity in ANY language
2. **Hate speech or discrimination** - STRICTLY REJECT any content that:
   - Discriminates based on race/ethnicity (""chỉ dành cho người da trắng"", ""không bán cho người đen"", ""for white people only"")
   - Discriminates based on religion (""chỉ dành cho người Hồi giáo"", ""cấm người Phật giáo"")
   - Discriminates based on nationality/region (""không bán cho người miền Nam"", ""chỉ dành cho người Việt"")
   - Discriminates based on gender (""chỉ dành cho nam"" is OK for fashion, but ""phụ nữ không được mua"" is NOT OK)
   - Uses derogatory terms for any group of people
3. **Sexual/Adult content** (Nội dung khiêu dâm, sexual harassment)
4. **Violence or threats** (Bạo lực, đe dọa, uy hiếp)
5. **Scam or fraud indicators** (Lừa đảo: giá quá rẻ, hàng fake)
6. **Spam or low-quality content**:
   - Random gibberish with no meaning
   - Not related to clothing/fashion
   - Keyboard spam (e.g., ""asdfgh"", ""qwerty"")
7. **Prohibited items** (Vũ khí, ma túy, thuốc không rõ nguồn gốc)
8. **Counterfeit brands** (Hàng giả: fake, replica, AAA, super fake, F1)

Product Name: {name}
Product Description: {description ?? "No description provided"}

CRITICAL EXAMPLES TO REJECT:

PROFANITY (MUST REJECT):
- Name: ""Fucking beautiful dress"" → REJECT (English profanity)
- Name: ""Shit quality but cheap"" → REJECT (profanity)
- Name: ""Áo đẹp vãi lồn"" → REJECT (Vietnamese profanity)
- Description: ""Chất lượng vãi lồn luôn"" → REJECT (profanity)
- Name: ""Đầm địt mẹ đẹp"" → REJECT (profanity)

DISCRIMINATION (MUST REJECT):
- Name: ""Trang phục chỉ dành cho người da trắng"" → REJECT (racial discrimination)
- Name: ""Áo không bán cho người đen"" → REJECT (racial discrimination)
- Description: ""Chỉ dành cho người giàu, nghèo đừng hỏi"" → REJECT (class discrimination)
- Name: ""Váy cho người miền Bắc, miền Nam cấm mua"" → REJECT (regional discrimination)

OTHER VIOLATIONS (MUST REJECT):
- Name: ""xyz"" with Description: ""abc def"" → REJECT (random gibberish)
- Name: ""Áo fake Gucci"" → REJECT (counterfeit)
- Name: ""Đầm sexy cho gái mại dâm"" → REJECT (sexual/adult content)
- Description: ""Giá rẻ chỉ 50k áo Gucci chính hãng"" → REJECT (scam)

EXAMPLES OF WHAT TO ACCEPT:
- Name: ""Áo sơ mi trắng"" → ACCEPT (""trắng"" refers to color, not race)
- Name: ""White shirt elegant"" → ACCEPT (""white"" is color)
- Name: ""Đầm dạ hội gợi cảm"" → ACCEPT (professional fashion term)
- Name: ""Áo dài cho nam"" → ACCEPT (gender targeting for fashion is normal)
- Name: ""Đầm cho nữ"" → ACCEPT (gender targeting for fashion is normal)

IMPORTANT RULES:
1. Be EXTREMELY STRICT on profanity - even ONE swear word = REJECT
2. Be EXTREMELY STRICT on discrimination - any hint of racial/religious bias = REJECT
3. Check BOTH product name AND description carefully
4. When in doubt, REJECT to protect users
5. This is a family-friendly platform - maintain high standards

TASK: Analyze the product name and description above. 
- Does it contain profanity? → REJECT
- Does it discriminate? → REJECT  
- Does it violate community guidelines? → REJECT
- Is it appropriate for a family-friendly platform? → If NO, REJECT

Respond ONLY in JSON format:
{{
    ""isAppropriate"": true/false,
    ""reason"": ""Brief explanation in English if inappropriate"",
    ""violatedTerms"": [""specific"", ""violated"", ""terms""]
}}

Remember: ONE violation = REJECT. Be strict.";
        }

        private async Task<string?> SendRequestToGeminiAsync(string prompt)
        {
            var apiKey = _moderationOptions.ApiKey;

            // Validate API key before making request
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Content moderation API key is not configured. " +
                    "Please update the API key in appsettings.json under 'ContentModeration' section. " +
                    "Get your API key at: https://aistudio.google.com/app/apikey"
                );
            }

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
                },
                // Safety settings to allow AI to analyze sensitive content
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.PostAsync(url, content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();

                    // Parse error response to provide clear messages
                    try
                    {
                        using var doc = JsonDocument.Parse(errorContent);
                        if (doc.RootElement.TryGetProperty("error", out var error))
                        {
                            var errorMessage = error.GetProperty("message").GetString();
                            var statusCode = (int)response.StatusCode;

                            // Handle specific error codes
                            if (statusCode == 429)
                            {
                                throw new InvalidOperationException(
                                    "API quota exceeded. The content moderation service has reached its daily limit. " +
                                    "Please wait 24 hours for quota reset or create a new API key at: https://aistudio.google.com/app/apikey. " +
                                    $"Details: {errorMessage}"
                                );
                            }
                            else if (statusCode == 400)
                            {
                                throw new InvalidOperationException(
                                    "Invalid API key or malformed request. " +
                                    "Please verify your API key in appsettings.json or create a new one at: https://aistudio.google.com/app/apikey. " +
                                    $"Details: {errorMessage}"
                                );
                            }
                            else if (statusCode == 403)
                            {
                                throw new InvalidOperationException(
                                    "Access forbidden. The API key does not have permission to access this service. " +
                                    "Please check your API key restrictions and ensure the Generative Language API is enabled. " +
                                    $"Details: {errorMessage}"
                                );
                            }
                            else if (statusCode == 404)
                            {
                                throw new InvalidOperationException(
                                    "Model 'gemini-2.0-flash' not found. " +
                                    "Try using a different model such as 'gemini-1.5-flash' or 'gemini-pro'. " +
                                    $"Details: {errorMessage}"
                                );
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    $"Gemini API error (HTTP {statusCode}): {errorMessage}"
                                );
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // If unable to parse JSON error response
                        var truncatedError = errorContent.Substring(0, Math.Min(200, errorContent.Length));
                        throw new InvalidOperationException(
                            $"Gemini API returned an error (HTTP {response.StatusCode}): {truncatedError}"
                        );
                    }

                    throw new InvalidOperationException($"Gemini API returned error: HTTP {response.StatusCode}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var responseDoc = JsonDocument.Parse(responseJson);

                // Check if response contains candidates
                if (!responseDoc.RootElement.TryGetProperty("candidates", out var candidates) ||
                    candidates.GetArrayLength() == 0)
                {
                    // Check if blocked by safety filters
                    if (responseDoc.RootElement.TryGetProperty("promptFeedback", out var feedback))
                    {
                        throw new InvalidOperationException(
                            "Content was blocked by AI safety filters. The submitted content may contain sensitive or inappropriate information."
                        );
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "AI service did not return any results. Please try again."
                        );
                    }
                }

                return candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException(
                    "Request to content moderation service timed out after 30 seconds. " +
                    "The service may be slow or unavailable. Please try again."
                );
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    "Network error connecting to content moderation service. " +
                    "Please check your internet connection and try again."
                );
            }
        }

        private ContentModerationResultDTO ParseModerationResponse(string response)
        {
            try
            {
                // Loại bỏ markdown code blocks nếu có
                var jsonResponse = response.Trim();
                if (jsonResponse.StartsWith("```json"))
                {
                    jsonResponse = jsonResponse.Substring(7);
                }
                if (jsonResponse.StartsWith("```"))
                {
                    jsonResponse = jsonResponse.Substring(3);
                }
                if (jsonResponse.EndsWith("```"))
                {
                    jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
                }
                jsonResponse = jsonResponse.Trim();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<ContentModerationResultDTO>(jsonResponse, options);

                return result ?? new ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null,
                    ViolatedTerms = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing moderation response: {Response}", response);
                // Default to allowing the content if parsing fails
                return new ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = "Unable to parse moderation result",
                    ViolatedTerms = new List<string>()
                };
            }
        }
    }
}

