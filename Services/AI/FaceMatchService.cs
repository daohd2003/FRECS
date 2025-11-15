using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Services.AI
{
    /// <summary>
    /// Service for FPT.AI Face Matching
    /// Compares if two faces belong to the same person
    /// </summary>
    public class FaceMatchService : IFaceMatchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FaceMatchService> _logger;
        private readonly string _apiKey;
        // FPT.AI Face Matching endpoint (Face Verify/Compare)
        // Docs: https://docs.fpt.ai/docs/en/vision/api/face-match
        private const string FPT_AI_FACEMATCH_URL = "https://api.fpt.ai/dmp/checkface/v1/";

        public FaceMatchService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<FaceMatchService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("FPTAI");
            _logger = logger;
            _apiKey = configuration["FptAI:ApiKey"] ?? throw new InvalidOperationException("FPT.AI API Key is not configured");
        }

        public async Task<FaceMatchResultDto> CompareFacesAsync(IFormFile selfieImage, IFormFile cccdImage)
        {
            if (selfieImage == null || selfieImage.Length == 0)
            {
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = "Selfie image is required"
                };
            }

            if (cccdImage == null || cccdImage.Length == 0)
            {
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = "CCCD image is required"
                };
            }

            // Validate file types
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedTypes.Contains(selfieImage.ContentType.ToLower()))
            {
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = "Invalid selfie image type. Only JPG/PNG are allowed"
                };
            }

            if (!allowedTypes.Contains(cccdImage.ContentType.ToLower()))
            {
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = "Invalid CCCD image type. Only JPG/PNG are allowed"
                };
            }

            // Validate file sizes (max 5MB each)
            if (selfieImage.Length > 5 * 1024 * 1024 || cccdImage.Length > 5 * 1024 * 1024)
            {
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = "Image size exceeds 5MB limit"
                };
            }

            try
            {
                using var content = new MultipartFormDataContent();

                // FPT.AI Face Match requires BOTH images in "file[]" array
                // Must send 2 files with same field name "file[]"

                // Add selfie image (file[0])
                using var selfieStream = selfieImage.OpenReadStream();
                using var selfieContent = new StreamContent(selfieStream);
                selfieContent.Headers.ContentType = new MediaTypeHeaderValue(selfieImage.ContentType);
                content.Add(selfieContent, "file[]", selfieImage.FileName ?? "selfie.jpg");

                // Add CCCD image (file[1])
                using var cccdStream = cccdImage.OpenReadStream();
                using var cccdContent = new StreamContent(cccdStream);
                cccdContent.Headers.ContentType = new MediaTypeHeaderValue(cccdImage.ContentType);
                content.Add(cccdContent, "file[]", cccdImage.FileName ?? "cccd.jpg");

                var request = new HttpRequestMessage(HttpMethod.Post, FPT_AI_FACEMATCH_URL);
                request.Headers.Add("api_key", _apiKey);
                request.Content = content;

                _logger.LogInformation("Calling FPT.AI FaceMatch API - Selfie: {SelfieFile}, CCCD: {CccdFile}",
                    selfieImage.FileName, cccdImage.FileName);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("FPT.AI FaceMatch Response Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FPT.AI FaceMatch API error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return new FaceMatchResultDto
                    {
                        IsMatched = false,
                        MatchScore = 0,
                        ErrorMessage = $"API Error: {response.StatusCode}"
                    };
                }

                // Parse response
                // FPT.AI FaceMatch response format:
                // {
                //   "data": {
                //     "is_match": true/false,
                //     "similarity": 0.95,
                //     "match_result": "MATCH"
                //   },
                //   "message": "success"
                // }
                var faceMatchResponse = JsonSerializer.Deserialize<JsonDocument>(responseContent);

                if (faceMatchResponse == null)
                {
                    return new FaceMatchResultDto
                    {
                        IsMatched = false,
                        MatchScore = 0,
                        ErrorMessage = "Failed to parse API response"
                    };
                }

                var root = faceMatchResponse.RootElement;

                // Check for error in response (code 409, 400, etc.)
                if (root.TryGetProperty("code", out var codeElement))
                {
                    var code = codeElement.GetString();
                    if (code != "200" && code != "0")
                    {
                        // Error response
                        string errorMsg = "Face matching failed";
                        if (root.TryGetProperty("data", out var dataErrElement) && dataErrElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            errorMsg = dataErrElement.GetString() ?? errorMsg;
                        }
                        else if (root.TryGetProperty("message", out var msgElement))
                        {
                            errorMsg = msgElement.GetString() ?? errorMsg;
                        }

                        _logger.LogError("FPT.AI FaceMatch API error code {Code}: {Error}", code, errorMsg);
                        return new FaceMatchResultDto
                        {
                            IsMatched = false,
                            MatchScore = 0,
                            ErrorMessage = $"API Error (Code {code}): {errorMsg}"
                        };
                    }
                }

                // Try to get data object (FPT.AI usually wraps response in "data")
                var dataElement = root;
                if (root.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    dataElement = dataObj;
                }

                // Try to get similarity score
                double similarity = 0;
                if (dataElement.TryGetProperty("similarity", out var similarityElement))
                {
                    similarity = similarityElement.GetDouble();
                }
                else if (dataElement.TryGetProperty("score", out var scoreElement))
                {
                    similarity = scoreElement.GetDouble();
                }
                else if (dataElement.TryGetProperty("confidence", out var confidenceElement))
                {
                    similarity = confidenceElement.GetDouble();
                }
                else if (dataElement.TryGetProperty("match_score", out var matchScoreElement))
                {
                    similarity = matchScoreElement.GetDouble();
                }

                // FPT.AI returns similarity in scale 0-100, normalize to 0-1
                // Example: 99.96 → 0.9996
                if (similarity > 1.0)
                {
                    similarity = similarity / 100.0;
                }

                // Check if faces match (threshold: 70% = 0.70)
                bool isMatch = similarity >= 0.70;

                string? errorMessage = null;
                if (root.TryGetProperty("message", out var messageElement))
                {
                    errorMessage = messageElement.GetString();
                }

                // Override isMatch from API if available
                if (dataElement.TryGetProperty("is_match", out var isMatchElement))
                {
                    var apiMatch = isMatchElement.GetBoolean();
                    // Use API result if it's more restrictive
                    if (!apiMatch) isMatch = false;
                }
                else if (dataElement.TryGetProperty("isMatch", out var isMatchElement2))
                {
                    var apiMatch = isMatchElement2.GetBoolean();
                    if (!apiMatch) isMatch = false;
                }
                else if (dataElement.TryGetProperty("match_result", out var matchResultElement))
                {
                    var matchResult = matchResultElement.GetString();
                    if (matchResult?.ToUpper() == "MATCH")
                    {
                        isMatch = true;
                    }
                    else if (matchResult?.ToUpper() == "NO_MATCH")
                    {
                        isMatch = false;
                    }
                }

                _logger.LogInformation("FaceMatch result - IsMatch: {IsMatch}, Similarity: {Similarity:P}",
                    isMatch, similarity);

                return new FaceMatchResultDto
                {
                    IsMatched = isMatch,
                    MatchScore = similarity,
                    ErrorMessage = isMatch ? null : (errorMessage ?? $"Face does not match CCCD photo. Similarity: {similarity:P}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling FPT.AI FaceMatch API");
                return new FaceMatchResultDto
                {
                    IsMatched = false,
                    MatchScore = 0,
                    ErrorMessage = $"Error: {ex.Message}"
                };
            }
        }
    }
}

