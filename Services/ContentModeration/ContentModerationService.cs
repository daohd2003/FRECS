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

        /// <summary>
        /// Check single content string (for Feedback, comments, etc.)
        /// Reuses the same AI moderation logic as product checking
        /// </summary>
        public async Task<ContentModerationResultDTO> CheckContentAsync(string content)
        {
            // Reuse existing logic - treat content as "name" with no description
            return await CheckProductContentAsync(content, null);
        }

        /// <summary>
        /// Check feedback content (comment + provider response)
        /// Can check either comment only, response only, or both
        /// Uses RELAXED moderation standards for feedback
        /// </summary>
        public async Task<ContentModerationResultDTO> CheckFeedbackContentAsync(string? comment, string? providerResponse = null)
        {
            // Combine both comment and response for checking
            var combinedContent = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(comment))
                combinedContent.Add($"Customer comment: {comment}");
            
            if (!string.IsNullOrWhiteSpace(providerResponse))
                combinedContent.Add($"Provider response: {providerResponse}");
            
            if (combinedContent.Count == 0)
                return new ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null,
                    ViolatedTerms = new List<string>()
                };
            
            var fullContent = string.Join(" | ", combinedContent);
            
            // Use relaxed moderation for feedback
            return await CheckFeedbackWithRelaxedStandardsAsync(fullContent);
        }
        
        private async Task<ContentModerationResultDTO> CheckFeedbackWithRelaxedStandardsAsync(string content)
        {
            try
            {
                // Pre-validation for feedback (catch obvious spam before AI)
                var (isValid, reason, violatedTerms) = PerformFeedbackPreValidation(content);
                if (!isValid)
                {
                    return new ContentModerationResultDTO
                    {
                        IsAppropriate = false,
                        Reason = reason,
                        ViolatedTerms = violatedTerms
                    };
                }
                
                // Check cache
                var cacheKey = $"feedback:{GenerateCacheKey(content, null)}";
                if (_cache.TryGetValue(cacheKey, out ContentModerationResultDTO? cachedResult))
                {
                    return cachedResult!;
                }

                var prompt = BuildRelaxedFeedbackPrompt(content);
                var geminiResponse = await SendRequestToGeminiAsync(prompt);

                if (string.IsNullOrEmpty(geminiResponse))
                {
                    // AI không response - dùng fallback analysis cho feedback
                    _logger.LogWarning("AI returned empty response for feedback. Using fallback analysis.");
                    var fallbackResult = AnalyzeFeedbackForFallback(content);
                    return new ContentModerationResultDTO
                    {
                        IsAppropriate = fallbackResult.IsAppropriate,
                        Reason = fallbackResult.Reason,
                        ViolatedTerms = fallbackResult.ViolatedTerms
                    };
                }

                var result = ParseModerationResponse(geminiResponse);

                // Cache for 7 days (increased from 24 hours for better performance)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(7))
                    .SetPriority(CacheItemPriority.Normal);

                _cache.Set(cacheKey, result, cacheOptions);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking feedback content");
                // Default to allowing feedback if error occurs
                return new ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null,
                    ViolatedTerms = new List<string>()
                };
            }
        }
        
        private string BuildRelaxedFeedbackPrompt(string content)
        {
            return $@"You are a content moderation system for customer feedback on an e-commerce platform.

CORE PRINCIPLE: Customers have the RIGHT to express opinions and complaints. Only block PROFANITY and THREATS.

REJECT ONLY if content contains:

1. **Severe Profanity** - REJECT if contains:
   - English profanity (f-word, s-word, b-word, etc.)
   - Vietnamese profanity (explicit sexual/vulgar terms)
   - Vietnamese abbreviations: cc, dm, đm, vcl, clm, cl, dcm, dcmm, vl, loz, lz, lol
   - Common variants and misspellings of above abbreviations
   - Personal insults with disrespectful pronouns (má mày, mày, tao as insult)
   - Direct insults combining profanity with negative words (như cc, đồ cc, etc.)
   
2. **Threats or Violence** - REJECT if contains:
   - Death threats: ""I will kill you"", ""Tao giết mày""
   - Violence: ""I will hurt you"", ""Đánh chết mày""
   
3. **Complete Gibberish** - REJECT if:
   - Random keyboard spam: ""asdfghjkl"", ""qwertyuiop""
   - No meaningful content at all

ACCEPT ALL OPINIONS AND COMPLAINTS including:
- ✅ ""Đồ lừa đảo"" (scam accusation - valid complaint)
- ✅ ""Hàng giả"" (fake product - valid complaint)
- ✅ ""Chất lượng tệ"" (bad quality - valid opinion)
- ✅ ""Seller lừa đảo"" (seller scam - valid complaint)
- ✅ ""Không đúng mô tả"" (not as described - valid complaint)
- ✅ ""Dịch vụ tệ"" (bad service - valid opinion)
- ✅ ""Không đáng tiền"" (not worth it - valid opinion)
- ✅ ""Thất vọng"" (disappointed - valid emotion)
- ✅ ""Tệ"", ""Kém"", ""Không tốt"" (negative opinions - valid)
- ✅ Short comments: ""Good"", ""Bad"", ""Ok"", ""Tốt"", ""Tệ""

Feedback Content: {content}

EXAMPLES TO ACCEPT:
- ""Đồ lừa đảo, hàng giả"" → ACCEPT (complaint about scam/fake)
- ""Chất lượng tệ, không đáng tiền"" → ACCEPT (quality complaint)
- ""Seller lừa đảo khách hàng"" → ACCEPT (service complaint)
- ""Hàng không đúng mô tả"" → ACCEPT (valid complaint)
- ""Thất vọng về sản phẩm"" → ACCEPT (expressing disappointment)
- ""Tệ"" → ACCEPT (short negative opinion)
- ""Not good"" → ACCEPT (short opinion)

EXAMPLES TO REJECT:
- Feedback with f-word directed at seller → REJECT (profanity + personal attack)
- Personal insults with disrespectful pronouns → REJECT (má mày, mày ngu)
- Death threats → REJECT (tao giết mày)
- Feedback with Vietnamese profanity abbreviations → REJECT (vcl, cc, dm, đm)
- Combining negative words with profanity → REJECT (như cc, đồ cc, hàng cc)
- Random keyboard spam → REJECT (asdfghjkl)

IMPORTANT: Vietnamese abbreviations (cc, dm, vcl, etc.) are VERY COMMON profanity shortcuts. ALWAYS REJECT feedback containing these abbreviations in negative context.

TASK: Check if feedback contains PROFANITY or THREATS. If NO → ACCEPT. If YES → REJECT.

Respond in JSON format:
{{
    ""isAppropriate"": true/false,
    ""reason"": ""Brief explanation if inappropriate"",
    ""violatedTerms"": [""specific"", ""profanity"", ""words""]
}}

Remember: ONLY block profanity and threats. ALL opinions and complaints are allowed.";
        }

        public async Task<ContentModerationResultDTO> CheckProductContentAsync(string name, string? description)
        {
            try
            {
                // Pre-validation check BEFORE calling AI (catches spam, repeating chars, etc.)
                var (isValid, reason, violatedTerms) = PerformPreValidation(name, description);

                if (!isValid)
                {
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
                    return cachedResult!;
                }

                var prompt = BuildModerationPrompt(name, description);

                // This will throw detailed exceptions if any error occurs
                var geminiResponse = await SendRequestToGeminiAsync(prompt);

                if (string.IsNullOrEmpty(geminiResponse))
                {
                    // Empty response - AI blocked content or service unavailable
                    // Use fallback analysis to provide specific reason
                    _logger.LogWarning("AI returned empty response for product: {Name} - {Description}. Using fallback analysis.", name, description);
                    
                    var fallbackResult = AnalyzeContentForFallback($"{name} {description}");
                    return new ContentModerationResultDTO
                    {
                        IsAppropriate = false,
                        Reason = fallbackResult.Reason,
                        ViolatedTerms = fallbackResult.ViolatedTerms
                    };
                }

                var result = ParseModerationResponse(geminiResponse);

                // Cache the result for 7 days (increased from 24 hours for better performance)
                // Longer cache = more cache hits = faster response time
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(7))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, result, cacheOptions);

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
            var combinedContent = $"{name} {description}".ToLower();

            // 0. Kiểm tra profanity và nội dung vi phạm TRƯỚC
            var profanityCheck = CheckForProfanityAndViolations(combinedContent);
            if (!profanityCheck.IsValid)
            {
                return profanityCheck;
            }

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

        /// <summary>
        /// Check for profanity, violence, discrimination, and other violations
        /// This runs BEFORE AI to catch obvious violations and provide specific error messages
        /// </summary>
        private (bool IsValid, string? Reason, List<string> ViolatedTerms) CheckForProfanityAndViolations(string content)
        {
            var lowerContent = content.ToLower();

            // Vietnamese profanity patterns (including bypass variants)
            var vietnameseProfanity = new Dictionary<string, string>
            {
                // vcl variants
                { @"\bvcl\b", "vcl" },
                { @"v\.c\.l", "v.c.l" },
                { @"v\-c\-l", "v-c-l" },
                { @"\bvl\b", "vl" },
                // dm variants
                { @"\bcc\b", "cc" },
                { @"\bdm\b", "dm" },
                { @"\bđm\b", "đm" },
                { @"đ\.m", "đ.m" },
                { @"d\.m", "d.m" },
                { @"\bdcm\b", "dcm" },
                { @"\bđcm\b", "đcm" },
                { @"\bdcmm\b", "dcmm" },
                { @"\bclm\b", "clm" },
                { @"\bcmnr\b", "cmnr" },
                // other profanity
                { @"\blol\b", "lol" },
                { @"\bloz\b", "loz" },
                { @"\bđịt\b", "địt" },
                { @"\bdit\b", "dit" },
                { @"đ\.t", "đ.t" },
                { @"\blồn\b", "lồn" },
                { @"\blon\b", "lon" },
                { @"\bđụ\b", "đụ" },
                { @"\bđù\b", "đù" },
                { @"\bcặc\b", "cặc" },
                { @"\bcac\b", "cac" },
                { @"\bbuồi\b", "buồi" },
                { @"\bđéo\b", "đéo" },
                { @"\bđệch\b", "đệch" },
                { @"\bvãi\b", "vãi" },
                { @"\bvãi lồn\b", "vãi lồn" },
                { @"\bvãi cả\b", "vãi cả" },
                { @"\bvkl\b", "vkl" },
            };

            // English profanity patterns (including bypass variants)
            var englishProfanity = new Dictionary<string, string>
            {
                // fuck variants
                { @"\bfuck\b", "fuck" },
                { @"\bfucking\b", "fucking" },
                { @"f\*ck", "f*ck" },
                { @"f\*cking", "f*cking" },
                { @"\bfvck\b", "fvck" },
                { @"\bfuk\b", "fuk" },
                { @"\bphuck\b", "phuck" },
                { @"\bfck\b", "fck" },
                // shit variants
                { @"\bshit\b", "shit" },
                { @"sh\*t", "sh*t" },
                { @"\bsh1t\b", "sh1t" },
                { @"\bsht\b", "sht" },
                // bitch variants
                { @"\bbitch\b", "bitch" },
                { @"b\*tch", "b*tch" },
                { @"\bb1tch\b", "b1tch" },
                { @"\bbiatch\b", "biatch" },
                // other profanity
                { @"\bass\b", "ass" },
                { @"\basshole\b", "asshole" },
                { @"\bdick\b", "dick" },
                { @"\bcock\b", "cock" },
                { @"\bpussy\b", "pussy" },
                { @"\bcunt\b", "cunt" },
                { @"\bbastard\b", "bastard" },
                { @"\bdamn\b", "damn" },
                { @"\bhell\b", "hell" },
                { @"\bwtf\b", "wtf" },
                { @"\bstfu\b", "stfu" },
            };

            // Violence/threat patterns (AI often blocks these)
            var violencePatterns = new Dictionary<string, string>
            {
                // English violence
                { @"\bi will kill\b", "i will kill" },
                { @"\bkill you\b", "kill you" },
                { @"\bkill myself\b", "kill myself" },
                { @"\bi(')?ll kill\b", "i'll kill" },
                { @"\bdie\b", "die" },
                { @"\bdeath\b", "death" },
                { @"\bmurder\b", "murder" },
                { @"\bsuicide\b", "suicide" },
                { @"\bhurt you\b", "hurt you" },
                { @"\bbuy or die\b", "buy or die" },
                // Vietnamese violence
                { @"\btao giết\b", "tao giết" },
                { @"\bgiết mày\b", "giết mày" },
                { @"\bgiết chết\b", "giết chết" },
                { @"\bđánh chết\b", "đánh chết" },
                { @"\bchết đi\b", "chết đi" },
                { @"\btự tử\b", "tự tử" },
                { @"\bbiết tay\b", "biết tay" },
            };

            // Discrimination patterns (AI often blocks these)
            var discriminationPatterns = new Dictionary<string, string>
            {
                // Racial discrimination
                { @"\bfor white people only\b", "for white people only" },
                { @"\bwhite people only\b", "white people only" },
                { @"\bblack people\b", "black people" },
                { @"\bngười đen\b", "người đen" },
                { @"\bngười da đen\b", "người da đen" },
                { @"\bkhông bán cho người đen\b", "không bán cho người đen" },
                { @"\bngười da trắng\b", "người da trắng" },
                { @"\bchỉ dành cho người da trắng\b", "chỉ dành cho người da trắng" },
                { @"\bda màu\b", "da màu" },
                // Implicit discrimination (ẩn ý phân biệt)
                { @"chỉ dành cho người có văn hóa", "chỉ dành cho người có văn hóa" },
                { @"chỉ dành cho người có gu", "chỉ dành cho người có gu" },
                { @"chỉ dành cho người có tiền", "chỉ dành cho người có tiền" },
                { @"không dành cho người nghèo", "không dành cho người nghèo" },
                { @"không bán cho người nghèo", "không bán cho người nghèo" },
                { @"không phù hợp với người nghèo", "không phù hợp với người nghèo" },
                { @"only for rich", "only for rich" },
                { @"only for classy", "only for classy" },
                { @"not for poor", "not for poor" },
                // Adult/Sexual content
                { @"\bgái mại dâm\b", "gái mại dâm" },
                { @"\bmại dâm\b", "mại dâm" },
                { @"\bgái gọi\b", "gái gọi" },
                { @"\bcave\b", "cave" },
                { @"\bđĩ\b", "đĩ" },
                { @"\bdi~\b", "di~" },
                { @"\bsex\b", "sex" },
                { @"\bporn\b", "porn" },
                { @"\bxxx\b", "xxx" },
                { @"\badult\b", "adult" },
                { @"\bnude\b", "nude" },
                { @"\bnaked\b", "naked" },
                { @"\bkhỏa thân\b", "khỏa thân" },
                { @"\bkích dục\b", "kích dục" },
                { @"\btình dục\b", "tình dục" },
                // Body shaming
                { @"\bngười béo\b", "người béo" },
                { @"\bngười mập\b", "người mập" },
                { @"\bkhông dành cho người béo\b", "không dành cho người béo" },
            };

            // Counterfeit patterns
            var counterfeitPatterns = new Dictionary<string, string>
            {
                { @"\bfake\b", "fake" },
                { @"\breplica\b", "replica" },
                { @"\bsuper fake\b", "super fake" },
                { @"\bhàng giả\b", "hàng giả" },
                { @"\bhàng nhái\b", "hàng nhái" },
                { @"\baaa\b", "AAA" },
                { @"\brep 1:1\b", "rep 1:1" },
                { @"\bhàng rep\b", "hàng rep" },
                { @"\bf1\b", "F1" },
            };

            // Check Vietnamese profanity
            foreach (var pattern in vietnameseProfanity)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return (false, $"Product contains inappropriate language: '{pattern.Value}'", new List<string> { pattern.Value });
                }
            }

            // Check English profanity
            foreach (var pattern in englishProfanity)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return (false, $"Product contains inappropriate language: '{pattern.Value}'", new List<string> { pattern.Value });
                }
            }

            // Check violence/threats
            foreach (var pattern in violencePatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return (false, $"Product contains violent or threatening content: '{pattern.Value}'", new List<string> { pattern.Value });
                }
            }

            // Check discrimination (use Contains for Vietnamese patterns, regex for English)
            foreach (var pattern in discriminationPatterns)
            {
                // For Vietnamese patterns, use Contains (regex \b doesn't work well with Vietnamese diacritics)
                var searchTerm = pattern.Value.ToLower();
                if (lowerContent.Contains(searchTerm))
                {
                    return (false, $"Product contains discriminatory content: '{pattern.Value}'", new List<string> { pattern.Value });
                }
            }

            // Check counterfeit
            foreach (var pattern in counterfeitPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return (false, $"Product appears to be counterfeit or fake: '{pattern.Value}'", new List<string> { pattern.Value });
                }
            }

            return (true, null, new List<string>());
        }

        private bool IsRepeatingCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Loại bỏ TẤT CẢ spaces và ký tự đặc biệt trước khi check
            var cleanText = new string(text.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLower();

            // Cần ít nhất 10 ký tự để check (tránh false positive với text ngắn)
            if (cleanText.Length < 10) return false;

            // Kiểm tra nếu có >80% ký tự giống nhau (bắt "111111", "aaaaaaa")
            var distinctChars = cleanText.Distinct().Count();
            var totalChars = cleanText.Length;

            // Nếu chỉ có 1-2 ký tự khác nhau trong chuỗi dài (>= 10 chars) → spam
            if (totalChars >= 10 && distinctChars <= 2)
            {
                return true;
            }

            // Kiểm tra pattern lặp liên tiếp: aaaaa, 11111, xxxxx
            // Chỉ bắt khi có >= 6 ký tự liên tiếp giống nhau (nới lỏng từ 4 lên 6)
            for (int i = 0; i < cleanText.Length - 2; i++)
            {
                if (cleanText[i] == cleanText[i + 1] && cleanText[i + 1] == cleanText[i + 2])
                {
                    // Đếm số ký tự liên tiếp giống nhau
                    int repeatCount = 3;
                    for (int j = i + 3; j < cleanText.Length && cleanText[j] == cleanText[i]; j++)
                    {
                        repeatCount++;
                    }

                    // Chỉ reject nếu lặp >= 6 lần (ví dụ: "aaaaaa")
                    if (repeatCount >= 6)
                    {
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
    ""violatedTerms"": [""exact words or phrases that violated the rules""]
}}

IMPORTANT for violatedTerms:
- Include the EXACT words/phrases from the product that caused the violation
- Example: if product contains ""i will kill you"", violatedTerms should be [""i will kill you""]
- Example: if product contains ""fuck"", violatedTerms should be [""fuck""]
- Do NOT put category names like ""Violence"" - put the actual violating text

Remember: ONE violation = REJECT. Be strict.";
        }

        private async Task<string?> SendRequestToGeminiAsync(string prompt)
        {
            var apiKey = _moderationOptions.ApiKey;
            var url = "https://openrouter.ai/api/v1/chat/completions";

            Console.WriteLine($"[OPENROUTER] Starting API call...");
            Console.WriteLine($"[OPENROUTER] API Key: {apiKey?.Substring(0, Math.Min(20, apiKey?.Length ?? 0))}...");

            var requestData = new
            {
                model = "google/gemini-2.5-flash-preview-09-2025",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 2048
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                Console.WriteLine($"[OPENROUTER] Sending request to {url}...");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.SendAsync(request, cts.Token);

                var responseJson = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OPENROUTER] Response: {response.StatusCode} - {responseJson.Substring(0, Math.Min(500, responseJson.Length))}");
                _logger.LogInformation("OpenRouter API response: {StatusCode} - {Response}", response.StatusCode, responseJson);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[OPENROUTER ERROR] {response.StatusCode} - {responseJson}");
                    _logger.LogError("OpenRouter API error: {StatusCode} - {Reason} - {Content}", response.StatusCode, response.ReasonPhrase, responseJson);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseJson);
                
                // Check for error in response body (OpenRouter sometimes returns 200 with error)
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMsg = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    _logger.LogError("OpenRouter returned error in response body: {Error}", errorMsg);
                    
                    // If blocked by safety filter, return a rejection response
                    if (errorMsg != null && (errorMsg.Contains("safety") || errorMsg.Contains("blocked") || errorMsg.Contains("harmful")))
                    {
                        return "{\"isAppropriate\": false, \"reason\": \"Content was blocked by AI safety filters due to potentially harmful content\", \"violatedTerms\": [\"harmful content detected\"]}";
                    }
                    return null;
                }

                // Check if choices array exists and has content
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    _logger.LogWarning("OpenRouter response has no choices");
                    return null;
                }

                var firstChoice = choices[0];
                
                // Check for finish_reason - if "content_filter" then content was blocked
                if (firstChoice.TryGetProperty("finish_reason", out var finishReason))
                {
                    var reason = finishReason.GetString();
                    if (reason == "content_filter" || reason == "safety")
                    {
                        _logger.LogWarning("Content was blocked by AI safety filter");
                        return "{\"isAppropriate\": false, \"reason\": \"Content was blocked by AI safety filters - likely contains threats, violence, or harmful content\", \"violatedTerms\": [\"harmful/violent content\"]}";
                    }
                }

                // Try to get message content safely
                string? result = null;
                try
                {
                    var message = firstChoice.GetProperty("message");
                    result = message.GetProperty("content").GetString();
                }
                catch (KeyNotFoundException)
                {
                    _logger.LogWarning("OpenRouter response missing message.content - likely blocked by safety filter");
                }
                
                // Check if content is null or empty (sign of safety filter blocking)
                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogWarning("OpenRouter returned empty content - likely blocked by safety filter. Full response: {Response}", responseJson);
                    // Return null to trigger fallback analysis in CheckProductContentAsync
                    return null;
                }
                
                _logger.LogInformation("OpenRouter API result: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling OpenRouter API: {Message} | StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                // Return null to let the caller handle it with proper error message
                return null;
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

        /// <summary>
        /// Fallback analysis when AI doesn't respond (blocked by safety filter)
        /// Analyzes content to provide specific reason and violated terms
        /// </summary>
        private (string Reason, List<string> ViolatedTerms) AnalyzeContentForFallback(string content)
        {
            var lowerContent = content.ToLower();
            var violatedTerms = new List<string>();
            var reasons = new List<string>();

            // 1. Check for discrimination patterns
            var discriminationPatterns = new Dictionary<string, string>
            {
                { @"không bán cho", "từ chối bán hàng" },
                { @"không dành cho", "loại trừ đối tượng" },
                { @"chỉ dành cho người", "phân biệt đối tượng" },
                { @"cấm người", "cấm đối tượng" },
                { @"người nghèo", "người nghèo" },
                { @"người giàu", "người giàu" },
                { @"người đen", "người đen" },
                { @"người da đen", "người da đen" },
                { @"người da trắng", "người da trắng" },
                { @"người da màu", "người da màu" },
                { @"for white", "for white" },
                { @"for black", "for black" },
                { @"white only", "white only" },
                { @"black people", "black people" },
                { @"người béo", "người béo" },
                { @"người mập", "người mập" },
                { @"người gầy", "người gầy" },
                { @"người xấu", "người xấu" },
                { @"người già", "người già" },
                { @"người trẻ", "người trẻ" },
                { @"miền bắc", "miền bắc" },
                { @"miền nam", "miền nam" },
                { @"miền trung", "miền trung" },
            };

            foreach (var pattern in discriminationPatterns)
            {
                if (lowerContent.Contains(pattern.Key))
                {
                    // Extract the full phrase containing the pattern
                    var startIndex = lowerContent.IndexOf(pattern.Key);
                    var endIndex = Math.Min(startIndex + 30, lowerContent.Length);
                    var phrase = content.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // Clean up phrase (remove trailing incomplete words)
                    var lastSpace = phrase.LastIndexOf(' ');
                    if (lastSpace > pattern.Key.Length)
                    {
                        phrase = phrase.Substring(0, lastSpace);
                    }
                    
                    violatedTerms.Add(phrase);
                    reasons.Add("phân biệt đối xử");
                }
            }

            // 2. Check for violence/threat patterns
            var violencePatterns = new Dictionary<string, string>
            {
                { @"giết", "đe dọa bạo lực" },
                { @"chết", "đe dọa bạo lực" },
                { @"đánh", "đe dọa bạo lực" },
                { @"kill", "đe dọa bạo lực" },
                { @"die", "đe dọa bạo lực" },
                { @"hurt", "đe dọa bạo lực" },
                { @"murder", "đe dọa bạo lực" },
                { @"biết tay", "đe dọa" },
                { @"hối hận", "đe dọa tâm lý" },
            };

            foreach (var pattern in violencePatterns)
            {
                if (lowerContent.Contains(pattern.Key))
                {
                    var startIndex = Math.Max(0, lowerContent.IndexOf(pattern.Key) - 10);
                    var endIndex = Math.Min(lowerContent.IndexOf(pattern.Key) + pattern.Key.Length + 15, lowerContent.Length);
                    var phrase = content.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    violatedTerms.Add(phrase);
                    reasons.Add(pattern.Value);
                }
            }

            // 3. Check for adult/sexual content
            var adultPatterns = new Dictionary<string, string>
            {
                { @"mại dâm", "nội dung người lớn" },
                { @"gái gọi", "nội dung người lớn" },
                { @"cave", "nội dung người lớn" },
                { @"đĩ", "nội dung người lớn" },
                { @"sex", "nội dung người lớn" },
                { @"porn", "nội dung người lớn" },
                { @"xxx", "nội dung người lớn" },
                { @"khỏa thân", "nội dung người lớn" },
                { @"nude", "nội dung người lớn" },
                { @"naked", "nội dung người lớn" },
                { @"kích dục", "nội dung người lớn" },
                { @"tình dục", "nội dung người lớn" },
            };

            foreach (var pattern in adultPatterns)
            {
                if (lowerContent.Contains(pattern.Key))
                {
                    var startIndex = Math.Max(0, lowerContent.IndexOf(pattern.Key) - 5);
                    var endIndex = Math.Min(lowerContent.IndexOf(pattern.Key) + pattern.Key.Length + 10, lowerContent.Length);
                    var phrase = content.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    violatedTerms.Add(phrase);
                    reasons.Add(pattern.Value);
                }
            }

            // 4. Check for scam indicators
            var scamPatterns = new Dictionary<string, string>
            {
                { @"chính hãng.*\d+k", "nghi ngờ lừa đảo (giá quá rẻ)" },
                { @"authentic.*\d+k", "nghi ngờ lừa đảo (giá quá rẻ)" },
                { @"original.*\d+k", "nghi ngờ lừa đảo (giá quá rẻ)" },
                { @"xách tay.*rẻ", "nghi ngờ lừa đảo" },
            };

            foreach (var pattern in scamPatterns)
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (regex.IsMatch(lowerContent))
                {
                    var match = regex.Match(lowerContent);
                    violatedTerms.Add(match.Value);
                    reasons.Add(pattern.Value);
                }
            }

            // Build final response
            if (violatedTerms.Count > 0)
            {
                var distinctReasons = reasons.Distinct().ToList();
                var reasonText = string.Join(", ", distinctReasons);
                return ($"Nội dung vi phạm: {reasonText}. Từ/cụm từ vi phạm: '{string.Join("', '", violatedTerms.Distinct())}'", violatedTerms.Distinct().ToList());
            }

            // Default fallback if no specific pattern found
            return ("Nội dung bị AI từ chối xử lý do có thể chứa nội dung không phù hợp. Vui lòng kiểm tra lại nội dung sản phẩm.", new List<string> { "content_blocked_by_ai" });
        }

        /// <summary>
        /// Fallback analysis for FEEDBACK when AI doesn't respond
        /// More lenient than product - only blocks severe violations
        /// ALLOWS: complaints about fake products, scams, bad service (valid feedback)
        /// BLOCKS: profanity, threats, harassment, discrimination
        /// </summary>
        private (bool IsAppropriate, string? Reason, List<string> ViolatedTerms) AnalyzeFeedbackForFallback(string content)
        {
            var lowerContent = content.ToLower();
            var violatedTerms = new List<string>();
            var reasons = new List<string>();

            // 1. Check for severe profanity (Vietnamese)
            var vietnameseProfanity = new Dictionary<string, string>
            {
                // Các từ viết tắt phổ biến
                { @"\bvcl\b", "vcl" },
                { @"\bvl\b", "vl" },
                { @"\bvkl\b", "vkl" },
                { @"\bcc\b", "cc" },
                { @"\bdm\b", "dm" },
                { @"\bđm\b", "đm" },
                { @"\bdcm\b", "dcm" },
                { @"\bđcm\b", "đcm" },
                { @"\bdcmm\b", "dcmm" },
                { @"\bđcmm\b", "đcmm" },
                { @"\bclm\b", "clm" },
                { @"\bcmnr\b", "cmnr" },
                { @"\bloz\b", "loz" },
                { @"\blz\b", "lz" },
                { @"\bcl\b", "cl" },
                // Các từ đầy đủ
                { @"\bđịt\b", "địt" },
                { @"\bdit\b", "dit" },
                { @"\blồn\b", "lồn" },
                { @"\blon\b", "lon" },
                { @"\bcặc\b", "cặc" },
                { @"\bcac\b", "cac" },
                { @"\bbuồi\b", "buồi" },
                { @"\bbuoi\b", "buoi" },
                { @"\bđụ\b", "đụ" },
                { @"\bdu\b", "du" },
                { @"\bđù\b", "đù" },
                { @"\bđéo\b", "đéo" },
                { @"\bdeo\b", "deo" },
                { @"\bđệch\b", "đệch" },
                { @"\bdech\b", "dech" },
                { @"\bvãi\b", "vãi" },
                { @"\bvai\b", "vai" },
                { @"\bchó\b", "chó" },
                { @"\bcho\b", "cho" },
                // Các biến thể có dấu chấm
                { @"v\.c\.l", "v.c.l" },
                { @"đ\.m", "đ.m" },
                { @"d\.m", "d.m" },
                { @"đ\.t", "đ.t" },
                { @"c\.c", "c.c" },
                // Các cụm từ tục
                { @"vãi lồn", "vãi lồn" },
                { @"vãi cả", "vãi cả" },
                { @"đồ chó", "đồ chó" },
                { @"con chó", "con chó" },
                { @"thằng chó", "thằng chó" },
                { @"con đĩ", "con đĩ" },
                { @"đồ đĩ", "đồ đĩ" },
                { @"mẹ mày", "mẹ mày" },
                { @"má mày", "má mày" },
                { @"bố mày", "bố mày" },
                { @"cha mày", "cha mày" },
                { @"cụ mày", "cụ mày" },
                { @"tổ cha", "tổ cha" },
                { @"tổ sư", "tổ sư" },
                { @"đồ ngu", "đồ ngu" },
                { @"thằng ngu", "thằng ngu" },
                { @"con ngu", "con ngu" },
                { @"đồ khốn", "đồ khốn" },
                { @"thằng khốn", "thằng khốn" },
                { @"đồ điên", "đồ điên" },
                { @"thằng điên", "thằng điên" },
            };

            foreach (var pattern in vietnameseProfanity)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    violatedTerms.Add(pattern.Value);
                    reasons.Add("ngôn ngữ tục tĩu");
                }
            }

            // 2. Check for severe profanity (English)
            var englishProfanity = new Dictionary<string, string>
            {
                // F-word variants
                { @"\bfuck\b", "fuck" },
                { @"\bfucking\b", "fucking" },
                { @"\bfucker\b", "fucker" },
                { @"\bfucked\b", "fucked" },
                { @"\bfck\b", "fck" },
                { @"\bfuk\b", "fuk" },
                { @"\bfvck\b", "fvck" },
                { @"\bphuck\b", "phuck" },
                { @"f\*ck", "f*ck" },
                { @"f\*cking", "f*cking" },
                // S-word variants
                { @"\bshit\b", "shit" },
                { @"\bshitty\b", "shitty" },
                { @"\bsh1t\b", "sh1t" },
                { @"\bsht\b", "sht" },
                { @"sh\*t", "sh*t" },
                // B-word variants
                { @"\bbitch\b", "bitch" },
                { @"\bbitches\b", "bitches" },
                { @"\bb1tch\b", "b1tch" },
                { @"\bbiatch\b", "biatch" },
                { @"b\*tch", "b*tch" },
                // Other profanity
                { @"\basshole\b", "asshole" },
                { @"\bass\b", "ass" },
                { @"\bcunt\b", "cunt" },
                { @"\bdick\b", "dick" },
                { @"\bdickhead\b", "dickhead" },
                { @"\bcock\b", "cock" },
                { @"\bpussy\b", "pussy" },
                { @"\bbastard\b", "bastard" },
                { @"\bdamn\b", "damn" },
                { @"\bdamned\b", "damned" },
                { @"\bwtf\b", "wtf" },
                { @"\bstfu\b", "stfu" },
                { @"\bmotherfucker\b", "motherfucker" },
                { @"\bmf\b", "mf" },
                { @"\bslut\b", "slut" },
                { @"\bwhore\b", "whore" },
                { @"\bidiot\b", "idiot" },
                { @"\bmoron\b", "moron" },
                { @"\bstupid\b", "stupid" },
                { @"\bdumbass\b", "dumbass" },
                { @"\bretard\b", "retard" },
                { @"\bretarded\b", "retarded" },
            };

            foreach (var pattern in englishProfanity)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern.Key, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    violatedTerms.Add(pattern.Value);
                    reasons.Add("ngôn ngữ tục tĩu");
                }
            }

            // 3. Check for threats/violence
            var threatPatterns = new Dictionary<string, string>
            {
                // Vietnamese threats
                { @"tao giết", "tao giết" },
                { @"giết mày", "giết mày" },
                { @"giết chết", "giết chết" },
                { @"đánh chết", "đánh chết" },
                { @"chém chết", "chém chết" },
                { @"đâm chết", "đâm chết" },
                { @"bắn chết", "bắn chết" },
                { @"biết nhà", "biết nhà" },
                { @"biết tay", "biết tay" },
                { @"liệu hồn", "liệu hồn" },
                { @"liệu mà", "liệu mà" },
                { @"coi chừng", "coi chừng" },
                { @"chờ đấy", "chờ đấy" },
                { @"đợi đấy", "đợi đấy" },
                { @"tìm đến nhà", "tìm đến nhà" },
                { @"tìm nhà", "tìm nhà" },
                { @"đốt shop", "đốt shop" },
                { @"phá shop", "phá shop" },
                { @"cho bom", "cho bom" },
                { @"đặt bom", "đặt bom" },
                { @"tự tử", "tự tử" },
                { @"muốn chết", "muốn chết" },
                // English threats
                { @"i will kill", "i will kill" },
                { @"i'll kill", "i'll kill" },
                { @"kill you", "kill you" },
                { @"hurt you", "hurt you" },
                { @"find you", "find you" },
                { @"come for you", "come for you" },
                { @"watch out", "watch out" },
                { @"you're dead", "you're dead" },
                { @"you are dead", "you are dead" },
                { @"gonna kill", "gonna kill" },
                { @"going to kill", "going to kill" },
                { @"beat you", "beat you" },
                { @"punch you", "punch you" },
                { @"burn your", "burn your" },
                { @"destroy your", "destroy your" },
            };

            foreach (var pattern in threatPatterns)
            {
                if (lowerContent.Contains(pattern.Key))
                {
                    violatedTerms.Add(pattern.Value);
                    reasons.Add("đe dọa bạo lực");
                }
            }

            // 4. Check for sexual harassment
            var harassmentPatterns = new Dictionary<string, string>
            {
                // Vietnamese harassment
                { @"cho số đi", "cho số đi" },
                { @"cho sdt", "cho sdt" },
                { @"cho zalo", "cho zalo" },
                { @"cho fb", "cho fb" },
                { @"cho facebook", "cho facebook" },
                { @"muốn hẹn hò", "muốn hẹn hò" },
                { @"muốn quen", "muốn quen" },
                { @"làm quen", "làm quen" },
                { @"kết bạn", "kết bạn" },
                { @"gái mại dâm", "gái mại dâm" },
                { @"mại dâm", "mại dâm" },
                { @"gái gọi", "gái gọi" },
                { @"cave", "cave" },
                { @"đĩ", "đĩ" },
                { @"điếm", "điếm" },
                { @"sex", "sex" },
                { @"xxx", "xxx" },
                { @"porn", "porn" },
                { @"khỏa thân", "khỏa thân" },
                { @"nude", "nude" },
                { @"naked", "naked" },
                { @"sexy quá", "sexy quá" },
                { @"gợi cảm quá", "gợi cảm quá" },
                { @"xinh quá cho", "xinh quá cho" },
                { @"đẹp quá cho", "đẹp quá cho" },
                // English harassment
                { @"give me your number", "give me your number" },
                { @"your number", "your number" },
                { @"wanna date", "wanna date" },
                { @"want to date", "want to date" },
                { @"hook up", "hook up" },
                { @"hot seller", "hot seller" },
                { @"sexy seller", "sexy seller" },
                { @"beautiful seller", "beautiful seller" },
                { @"prostitute", "prostitute" },
                { @"escort", "escort" },
            };

            foreach (var pattern in harassmentPatterns)
            {
                if (lowerContent.Contains(pattern.Key))
                {
                    violatedTerms.Add(pattern.Value);
                    reasons.Add("quấy rối");
                }
            }

            // 5. Check for discrimination patterns
            // Block discrimination in feedback
            var discriminationWithInsult = new[]
            {
                // Vietnamese racial discrimination - explicit phrases
                @"chỉ dành cho người da trắng",
                @"chỉ dành cho người da đen",
                @"không dành cho người da trắng",
                @"không dành cho người da đen",
                @"người da trắng",
                @"người da đen",
                @"người đen",
                @"da trắng mới được",
                @"da đen không được",
                @"da màu",
                @"da mau",
                // Vietnamese racial discrimination - implicit
                @"người đen.*nên.*tệ",
                @"người đen.*nên.*xấu",
                @"người đen.*nên.*dở",
                @"da đen.*nên.*tệ",
                @"da đen.*nên.*xấu",
                @"là người đen",
                @"vì.*người đen",
                @"tại.*người đen",
                @"do.*người đen",
                @"người da trắng.*tốt hơn",
                @"người da đen.*tệ hơn",
                // English racial discrimination
                @"black.*so.*bad",
                @"black.*terrible",
                @"because.*black",
                @"white.*better",
                @"black.*worse",
                @"racist",
                @"n[i1]gg[ae]r",
                @"negro",
                @"chink",
                @"gook",
                // Vietnamese regional discrimination
                @"người miền bắc.*tệ",
                @"người miền nam.*tệ",
                @"người miền trung.*tệ",
                @"dân bắc.*tệ",
                @"dân nam.*tệ",
                @"ghét.*miền bắc",
                @"ghét.*miền nam",
                // Vietnamese religious discrimination
                @"người theo đạo.*tệ",
                @"người công giáo.*tệ",
                @"người phật giáo.*tệ",
                @"người hồi giáo.*tệ",
                // Body shaming with insult
                @"người béo.*tệ",
                @"người mập.*tệ",
                @"người gầy.*tệ",
                @"đồ béo",
                @"đồ mập",
                @"đồ gầy",
                @"fat.*ugly",
                @"skinny.*ugly",
                // Gender discrimination
                @"đàn bà.*ngu",
                @"phụ nữ.*ngu",
                @"đàn ông.*ngu",
                @"women.*stupid",
                @"men.*stupid",
                // LGBTQ discrimination
                @"đồ gay",
                @"thằng gay",
                @"con gay",
                @"đồ les",
                @"đồ bê đê",
                @"thằng bê đê",
                @"faggot",
                @"fag",
                @"dyke",
                @"tranny",
            };

            foreach (var pattern in discriminationWithInsult)
            {
                // For simple Vietnamese phrases, use Contains (regex \b doesn't work with Vietnamese)
                // For patterns with .* (regex), use Regex.IsMatch
                if (pattern.Contains(".*"))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(lowerContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(lowerContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        violatedTerms.Add(match.Value);
                        reasons.Add("phân biệt đối xử");
                    }
                }
                else
                {
                    // Use Contains for simple phrases
                    if (lowerContent.Contains(pattern.ToLower()))
                    {
                        violatedTerms.Add(pattern);
                        reasons.Add("phân biệt đối xử");
                    }
                }
            }

            // Build result
            if (violatedTerms.Count > 0)
            {
                var distinctReasons = reasons.Distinct().ToList();
                var reasonText = string.Join(", ", distinctReasons);
                return (false, $"Feedback vi phạm: {reasonText}. Từ/cụm từ vi phạm: '{string.Join("', '", violatedTerms.Distinct())}'", violatedTerms.Distinct().ToList());
            }

            // No severe violation found - ALLOW feedback
            // This is more lenient than product moderation
            return (true, null, new List<string>());
        }

        /// <summary>
        /// Pre-validation for feedback content to catch obvious spam before calling AI
        /// More lenient than product validation (allows shorter content, focuses on spam detection)
        /// </summary>
        private (bool IsValid, string? Reason, List<string> ViolatedTerms) PerformFeedbackPreValidation(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return (true, null, new List<string>()); // Empty feedback is OK (will be caught by DTO validation)
            }

            var trimmedContent = content.Trim();
            var lowerContent = trimmedContent.ToLower();
            
            // Remove spaces to catch bypass like "con ch ó" → "conchó"
            var noSpaceContent = lowerContent.Replace(" ", "");

            // 1. Check for Vietnamese profanity (pre-validation before AI)
            // Using Contains to catch concatenated words (e.g., "hàngvcl", "đẹpđm")
            // Includes both spaced, non-spaced, and non-diacritics versions
            var vietnameseProfanityWords = new[] { 
                // Abbreviations
                "vcl", "vkl", "vl", "cc", "dm", "đm", "dcm", "đcm", "dcmm", "đcmm", "clm", "cmnr", "loz", "lz", "cl",
                // Full words (with diacritics)
                "địt", "lồn", "đụ", "đù", "cặc", "buồi", "đéo", "đệch",
                // Full words (without diacritics - bypass prevention)
                "dit", "lon", "du", "cac", "buoi", "deo", "dech",
                // Phrases with space (with diacritics)
                "vãi lồn", "đồ chó", "con chó", "thằng chó", "con đĩ", "mẹ mày", "má mày", "bố mày", "tổ cha",
                // Phrases WITHOUT space (with diacritics)
                "mẹmày", "mámày", "bốmày", "chamày", "cụmày", "tổcha", "đồchó", "conchó", "thằngchó", "conđĩ",
                "đồngu", "thằngngu", "conngu", "đồkhốn", "thằngkhốn", "đồđiên", "thằngđiên",
                // Phrases WITHOUT diacritics (bypass prevention)
                "memay", "mamay", "bomay", "chamay", "cumay", "tocha",
                "docho", "concho", "thangcho", "condi",
                "dongu", "thangngu", "conngu", "dokhon", "thangkhon", "dodien", "thangdien",
                // Common insult patterns without diacritics
                "conmemay", "conmamay", "thangmemay", "thangmamay",
                "ditme", "ditmemay", "ditcon", "lonme", "caime", "ducme", "dume",
                // "cm" abbreviation patterns (con mẹ)
                "cmmày", "cmmay", "cmm", "nhưcm", "nhucm", "nhumcm"
            };
            
            foreach (var word in vietnameseProfanityWords)
            {
                // Check both original content and no-space version to catch bypass like "con ch ó"
                if (lowerContent.Contains(word) || noSpaceContent.Contains(word.Replace(" ", "")))
                {
                    return (false, $"Feedback contains inappropriate language: '{word}'", new List<string> { "profanity", word });
                }
            }

            // 1.5. Check for English profanity (without word boundary to catch concatenated words)
            var englishProfanityWords = new[] { "fuck", "fucking", "fucker", "fck", "fuk", "shit", "shitty", "bitch", "bitches", "asshole", "ass", "cunt", "dick", "cock", "pussy", "bastard", "damn", "wtf", "stfu", "motherfucker", "slut", "whore" };
            
            foreach (var word in englishProfanityWords)
            {
                // Check both original content and no-space version
                if (lowerContent.Contains(word) || noSpaceContent.Contains(word))
                {
                    return (false, $"Feedback contains inappropriate language: '{word}'", new List<string> { "profanity", word });
                }
            }

            // 1.6. Check for discrimination patterns (racial, etc.)
            var discriminationWords = new[] {
                "người da trắng", "người da đen", "người đen", "da màu", "da mau",
                "nguoi da trang", "nguoi da den", "nguoi den",
                "chỉ dành cho người da", "chi danh cho nguoi da",
                "không dành cho người da", "khong danh cho nguoi da",
                "for white people", "for black people", "white only", "black only",
                "racist", "negro", "nigger", "chink", "gook"
            };
            
            foreach (var word in discriminationWords)
            {
                if (lowerContent.Contains(word) || noSpaceContent.Contains(word.Replace(" ", "")))
                {
                    return (false, $"Feedback contains discriminatory content: '{word}'", new List<string> { "discrimination", word });
                }
            }
            
            // 2. Check for personal insults with disrespectful pronouns
            var insultPatterns = new[]
            {
                @"\bmẹ\s+mày\b",      // "mẹ mày"
                @"\bmá\s+mày\b",      // "má mày"
                @"\bcon\s+mẹ\b",      // "con mẹ"
                @"\bcon\s+má\b",      // "con má"
                @"\bmày\s+ngu\b",     // "mày ngu"
                @"\bmày\s+đần\b",     // "mày đần"
                @"\bnhư\s+c\b",       // "như c" (như cặc)
                @"\bđồ\s+c\b",        // "đồ c" (đồ cặc)
                @"\bhàng\s+c\b",      // "hàng c" (hàng cặc)
                @"\bshop\s+ngu\b",    // "shop ngu"
                @"\bshop\s+ốc\b",     // "shop ốc" (ốc = ngu)
                @"\bshop\s+óc\b",     // "shop óc" (variant)
                @"\bshop\s+đần\b",    // "shop đần"
                @"\bshop\s+ngu\s+\w+\b", // "shop ngu thế", "shop ngu vl"
                @"\bshop\s+ốc\s+\w+\b",  // "shop ốc thế", "shop ốc vl"
                @"\bseller\s+ngu\b",  // "seller ngu"
                @"\bseller\s+ốc\b",   // "seller ốc"
                @"\bseller\s+đần\b"   // "seller đần"
            };
            
            foreach (var pattern in insultPatterns)
            {
                var insultRegex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (insultRegex.IsMatch(lowerContent))
                {
                    var match = insultRegex.Match(lowerContent);
                    return (false, $"Feedback contains personal insults or disrespectful language", new List<string> { "insult", match.Value });
                }
            }

            // 3. Check for excessive repeating characters (spam detection)
            if (IsRepeatingCharacters(trimmedContent))
            {
                return (false, "Feedback contains excessive repeating characters or spam patterns", new List<string> { "repeating characters" });
            }

            // 4. Check if content is ONLY gibberish (no meaningful words at all)
            // More lenient: Allow if there's at least SOME meaningful content
            if (IsCompleteGibberish(trimmedContent))
            {
                return (false, "Feedback appears to be random gibberish with no meaningful content", new List<string> { "gibberish" });
            }

            return (true, null, new List<string>());
        }

        /// <summary>
        /// Check if content is COMPLETE gibberish (no meaningful words at all)
        /// More lenient than product validation - allows mixed content
        /// </summary>
        private bool IsCompleteGibberish(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Remove spaces and special characters
            var cleanText = new string(text.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            if (cleanText.Length < 10) return false; // Short text is OK

            // Check if text has at least SOME recognizable patterns
            // If it's mostly random keyboard patterns, it's gibberish
            
            // Common keyboard patterns to detect
            var keyboardPatterns = new[] {
                "asdfgh", "qwerty", "zxcvbn", "hjkl", "uiop",
                "asdf", "qwer", "zxcv", "hjkl", "uiop"
            };

            var lowerText = cleanText.ToLower();
            var hasKeyboardPattern = keyboardPatterns.Any(pattern => lowerText.Contains(pattern));

            // If has keyboard pattern AND text is mostly non-alphabetic, it's gibberish
            if (hasKeyboardPattern)
            {
                var letterCount = cleanText.Count(c => char.IsLetter(c));
                var digitCount = cleanText.Count(c => char.IsDigit(c));
                
                // If more than 50% is keyboard spam pattern, reject
                var keyboardSpamLength = keyboardPatterns
                    .Where(p => lowerText.Contains(p))
                    .Sum(p => p.Length);
                
                if (keyboardSpamLength > cleanText.Length * 0.5)
                {
                    return true; // Mostly keyboard spam
                }
            }

            return false;
        }
    }
}

