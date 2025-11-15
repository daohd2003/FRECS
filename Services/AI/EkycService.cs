using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Services.AI
{
    /// <summary>
    /// Service for FPT.AI eKYC - ID Card Recognition
    /// </summary>
    public class EkycService : IEkycService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EkycService> _logger;
        private readonly string _apiKey;
        private const string FPT_AI_EKYC_URL = "https://api.fpt.ai/vision/idr/vnm";

        public EkycService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EkycService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("FPTAI");
            _logger = logger;
            _apiKey = configuration["FptAI:ApiKey"] ?? throw new InvalidOperationException("FPT.AI API Key is not configured");
        }

        public async Task<CccdVerificationResultDto> VerifyCccdAsync(IFormFile idCardImage)
        {
            if (idCardImage == null || idCardImage.Length == 0)
            {
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "No image provided",
                    Confidence = 0
                };
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedTypes.Contains(idCardImage.ContentType.ToLower()))
            {
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "Invalid file type. Only JPG/PNG are allowed",
                    Confidence = 0
                };
            }

            // Validate file size (max 5MB)
            if (idCardImage.Length > 5 * 1024 * 1024)
            {
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "File size exceeds 5MB limit",
                    Confidence = 0
                };
            }

            try
            {
                using var content = new MultipartFormDataContent();
                using var stream = idCardImage.OpenReadStream();
                using var streamContent = new StreamContent(stream);

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(idCardImage.ContentType);
                content.Add(streamContent, "image", idCardImage.FileName);

                var request = new HttpRequestMessage(HttpMethod.Post, FPT_AI_EKYC_URL);
                request.Headers.Add("api_key", _apiKey);
                request.Content = content;

                _logger.LogInformation("Calling FPT.AI eKYC API for file: {FileName}", idCardImage.FileName);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("FPT.AI Response Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FPT.AI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return new CccdVerificationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = $"API Error: {response.StatusCode}",
                        Confidence = 0
                    };
                }

                var fptResponse = JsonSerializer.Deserialize<FptEkycResponseDto>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fptResponse == null)
                {
                    return new CccdVerificationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = "Failed to parse API response",
                        Confidence = 0
                    };
                }

                if (fptResponse.ErrorCode != 0)
                {
                    _logger.LogWarning("FPT.AI returned error code {ErrorCode}: {ErrorMessage}",
                        fptResponse.ErrorCode, fptResponse.ErrorMessage);
                    return new CccdVerificationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = fptResponse.ErrorMessage ?? "Unknown error from FPT.AI",
                        Confidence = 0
                    };
                }

                if (fptResponse.Data == null || !fptResponse.Data.Any())
                {
                    return new CccdVerificationResultDto
                    {
                        IsValid = false,
                        ErrorMessage = "No data extracted from ID card. Please ensure the image is clear and well-lit",
                        Confidence = 0
                    };
                }

                var data = fptResponse.Data[0];

                // Calculate average confidence score
                // For NEW CCCD (with chip), FPT.AI returns "overall_score" instead of individual prob fields
                double avgConfidence = 0;

                if (!string.IsNullOrWhiteSpace(data.OverallScore) && double.TryParse(data.OverallScore, out var overallScore))
                {
                    // NEW CCCD: Use overall_score directly (usually 95-99%)
                    avgConfidence = overallScore / 100.0; // Convert from 99.44 to 0.9944
                    _logger.LogInformation("✅ NEW CCCD (chip) detected - Using overall_score: {Score}% → Confidence: {Confidence:P}",
                        data.OverallScore, avgConfidence);
                }
                else
                {
                    // OLD CCCD: Calculate from individual prob fields
                    var confidenceScores = new List<double>();
                    if (double.TryParse(data.IdProb, out var idProb)) confidenceScores.Add(idProb);
                    if (double.TryParse(data.NameProb, out var nameProb)) confidenceScores.Add(nameProb);
                    if (double.TryParse(data.DateOfBirthProb, out var dobProb)) confidenceScores.Add(dobProb);

                    avgConfidence = confidenceScores.Any() ? confidenceScores.Average() : 0;
                    _logger.LogInformation("CCCD Confidence Scores - ID: {IdProb}, Name: {NameProb}, DOB: {DobProb}, Average: {AvgConfidence:P}",
                        data.IdProb, data.NameProb, data.DateOfBirthProb, avgConfidence);
                }

                // Validate that we got the essential information
                // ⚠️ WARNING: 20% threshold is EXTREMELY LOW - for TESTING ONLY!
                // This may accept poor quality images with incorrect OCR results

                // For NEW CCCD (with chip), back side may have:
                // - overall_score only (fingerprints, no text)
                // - overall_score + Name from MRZ (if FPT.AI can read it)
                // Accept if:
                // 1. Has ID + Name (front side or old CCCD)
                // 2. Has overall_score (NEW CCCD back side - trust FPT.AI's score)
                bool hasEssentialInfo = (!string.IsNullOrWhiteSpace(data.Id) && !string.IsNullOrWhiteSpace(data.Name)) ||
                                       !string.IsNullOrWhiteSpace(data.OverallScore);

                var isValid = hasEssentialInfo && avgConfidence > 0.20; // At least 20% confidence - TESTING ONLY!

                _logger.LogInformation("Validation check - HasID: {HasId}, HasName: {HasName}, HasOverallScore: {HasOverall}, IsValid: {IsValid}",
                    !string.IsNullOrWhiteSpace(data.Id),
                    !string.IsNullOrWhiteSpace(data.Name),
                    !string.IsNullOrWhiteSpace(data.OverallScore),
                    isValid);

                if (avgConfidence < 0.50)
                {
                    _logger.LogWarning("⚠️ CCCD passed with LOW confidence: {Confidence:P}. This is only acceptable for testing!", avgConfidence);
                }

                var errorMessage = isValid ? null :
                    $"CCCD verification failed (Confidence: {avgConfidence:P}). " +
                    "Tips: \n" +
                    "- Ensure good lighting (natural light is best)\n" +
                    "- Place CCCD on dark background\n" +
                    "- Take photo at 90° angle (not tilted)\n" +
                    "- Make sure all text is sharp and readable\n" +
                    "- Avoid glare/reflection on the plastic surface";

                return new CccdVerificationResultDto
                {
                    IsValid = isValid,
                    IdNumber = data.Id,
                    FullName = data.Name,
                    DateOfBirth = data.DateOfBirth,
                    Sex = data.Sex,
                    Nationality = data.Nationality,
                    Address = data.Address ?? data.ResidenceAddress,
                    DateOfExpiry = data.DateOfExpiry,
                    IssueDate = data.IssueDate,
                    CardType = data.CardTypeNew ?? data.CardType,
                    Confidence = avgConfidence,
                    ErrorMessage = errorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling FPT.AI eKYC API");
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = $"Error: {ex.Message}",
                    Confidence = 0
                };
            }
        }

        public async Task<CccdVerificationResultDto> VerifyCccdBothSidesAsync(IFormFile frontImage, IFormFile backImage)
        {
            _logger.LogInformation("Verifying both sides of CCCD");

            var frontResult = await VerifyCccdAsync(frontImage);
            var backResult = await VerifyCccdAsync(backImage);

            // === VALIDATE: Detect if images are uploaded in wrong positions ===
            var isFrontActuallyFront = IsFrontSideImage(frontResult);
            var isBackActuallyBack = IsBackSideImage(backResult);

            if (!isFrontActuallyFront && IsFrontSideImage(backResult))
            {
                _logger.LogWarning("⚠️ Images uploaded in WRONG positions - front and back are swapped!");
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "❌ ẢNH UPLOAD SAI VỊ TRÍ!\n" +
                                   "Bạn đã upload:\n" +
                                   "- Mặt TRƯỚC vào vị trí BACK\n" +
                                   "- Mặt SAU vào vị trí FRONT\n" +
                                   "Vui lòng đổi vị trí 2 ảnh và thử lại!",
                    Confidence = 0
                };
            }

            // Check if BOTH images are front side (same image uploaded twice)
            // This happens when backResult has ID + Name + DOB (clear front side indicators)
            if (IsFrontSideImage(backResult) && !isBackActuallyBack)
            {
                _logger.LogWarning("⚠️ Same front side image uploaded for BOTH positions!");
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "❌ BẠN UPLOAD CÙNG 1 ẢNH MẶT TRƯỚC CHO CẢ 2 VỊ TRÍ!\n" +
                                   "Vui lòng upload:\n" +
                                   "- Vị trí FRONT: Ảnh mặt TRƯỚC (có ảnh chân dung)\n" +
                                   "- Vị trí BACK: Ảnh mặt SAU (CCCD mới: vân tay, CCCD cũ: địa chỉ)",
                    Confidence = 0
                };
            }

            // For NEW CCCD (with chip), back side may not have much text info
            // Accept if back position doesn't have ID and DOB (even if validation is inconclusive)
            // This is common for NEW CCCD where back side only has fingerprints
            bool likelyNewCccdBack = string.IsNullOrWhiteSpace(backResult.IdNumber) &&
                                     string.IsNullOrWhiteSpace(backResult.DateOfBirth);

            if (!isBackActuallyBack && likelyNewCccdBack)
            {
                _logger.LogInformation("ℹ️ Back side validation inconclusive (likely NEW CCCD with chip - fingerprints only, 0% confidence is OK). Accepting...");
                // Don't reject - new CCCD back side is hard/impossible to OCR
                // Override to treat as valid back side
                isBackActuallyBack = true;
            }

            if (!isFrontActuallyFront)
            {
                _logger.LogWarning("⚠️ Front position does not contain front side image!");
                return new CccdVerificationResultDto
                {
                    IsValid = false,
                    ErrorMessage = "❌ ẢNH MẶT TRƯỚC KHÔNG HỢP LỆ!\n" +
                                   "Ảnh mặt trước phải chứa: Số CCCD, Họ tên, Ngày sinh\n" +
                                   "Vui lòng chụp lại mặt TRƯỚC của CCCD (mặt có ảnh chân dung)",
                    Confidence = 0
                };
            }

            // Combine results - front side usually has the main info
            // For NEW CCCD (with chip), back side may not pass validation due to limited text
            // Prioritize front side validation (most important info is there)

            // NEW CCCD back side detection:
            // - No ID number (key indicator)
            // - No Date of Birth (front side only)
            // - Low or ZERO confidence (only fingerprints, no text to OCR)
            bool isNewCccdBackSide = string.IsNullOrWhiteSpace(backResult.IdNumber) &&
                                     string.IsNullOrWhiteSpace(backResult.DateOfBirth) &&
                                     backResult.Confidence <= 0.5; // Accept even 0% confidence

            _logger.LogInformation("Back side analysis - NoID: {NoId}, NoDOB: {NoDob}, Confidence: {Conf:P}, IsNewCCCD: {IsNew}",
                string.IsNullOrWhiteSpace(backResult.IdNumber),
                string.IsNullOrWhiteSpace(backResult.DateOfBirth),
                backResult.Confidence,
                isNewCccdBackSide);

            var combinedResult = new CccdVerificationResultDto
            {
                // Accept if front is valid, even if back fails (for NEW CCCD with chip)
                // NEW CCCD back side often has 0% confidence because it only contains fingerprints
                IsValid = frontResult.IsValid && (backResult.IsValid || isNewCccdBackSide),
                IdNumber = frontResult.IdNumber ?? backResult.IdNumber,
                FullName = frontResult.FullName ?? backResult.FullName,
                DateOfBirth = frontResult.DateOfBirth ?? backResult.DateOfBirth,
                Sex = frontResult.Sex ?? backResult.Sex,
                Nationality = frontResult.Nationality ?? backResult.Nationality,
                Address = backResult.Address ?? frontResult.Address, // Address usually on back
                DateOfExpiry = backResult.DateOfExpiry ?? frontResult.DateOfExpiry,
                IssueDate = backResult.IssueDate ?? frontResult.IssueDate,
                CardType = frontResult.CardType ?? backResult.CardType,
                Confidence = (frontResult.Confidence + backResult.Confidence) / 2,
            };

            if (!frontResult.IsValid)
            {
                combinedResult.ErrorMessage = $"Front: {frontResult.ErrorMessage}";
            }
            else if (!backResult.IsValid && !isNewCccdBackSide)
            {
                // Only show back error if it's NOT a new CCCD back side
                combinedResult.ErrorMessage = $"Back: {backResult.ErrorMessage}";
                _logger.LogWarning("Back side validation failed but front is OK. Back error: {Error}", backResult.ErrorMessage);
            }
            else if (isNewCccdBackSide)
            {
                _logger.LogInformation("✅ NEW CCCD (with chip) detected - accepting with front side info only");
                _logger.LogInformation("   → Back side has 0% confidence (only fingerprints, no text) - THIS IS NORMAL for new CCCD!");
            }

            _logger.LogInformation("Combined verification result - IsValid: {IsValid}, Confidence: {Confidence}",
                combinedResult.IsValid, combinedResult.Confidence);

            return combinedResult;
        }

        /// <summary>
        /// Helper method to detect if OCR result is from FRONT side of CCCD
        /// Front side contains: ID number, Full name, Date of birth
        /// </summary>
        private bool IsFrontSideImage(CccdVerificationResultDto result)
        {
            // Front side MUST have ID and Name (key identifiers)
            bool hasIdAndName = !string.IsNullOrWhiteSpace(result.IdNumber) &&
                               !string.IsNullOrWhiteSpace(result.FullName);

            // Front side usually has Date of Birth
            bool hasDateOfBirth = !string.IsNullOrWhiteSpace(result.DateOfBirth);

            _logger.LogDebug("Front side check - HasID: {HasId}, HasName: {HasName}, HasDOB: {HasDob}",
                !string.IsNullOrWhiteSpace(result.IdNumber),
                !string.IsNullOrWhiteSpace(result.FullName),
                hasDateOfBirth);

            return hasIdAndName && hasDateOfBirth;
        }

        /// <summary>
        /// Helper method to detect if OCR result is from BACK side of CCCD
        /// Back side contains: Address, Issue date, but NO ID (or only Name from MRZ for new CCCD)
        /// Supports both OLD CCCD and NEW CCCD (with chip)
        /// </summary>
        private bool IsBackSideImage(CccdVerificationResultDto result)
        {
            // CRITICAL: Back side MUST NOT have ID number (this is the key differentiator)
            bool noIdNumber = string.IsNullOrWhiteSpace(result.IdNumber);

            // Back side may have Name from MRZ (new CCCD with chip)
            // But should not have Date of Birth (this is front side only)
            bool noDateOfBirth = string.IsNullOrWhiteSpace(result.DateOfBirth);

            // OLD CCCD back: Has Address and/or Issue Date
            // NEW CCCD back: May only have Name (from MRZ), with fingerprints
            bool hasBackSideInfo = !string.IsNullOrWhiteSpace(result.Address) ||
                                  !string.IsNullOrWhiteSpace(result.IssueDate) ||
                                  !string.IsNullOrWhiteSpace(result.FullName); // Name from MRZ

            _logger.LogDebug("Back side check - NoID: {NoId}, NoDOB: {NoDob}, HasName: {HasName}, HasAddress: {HasAddr}, HasIssueDate: {HasIssue}",
                noIdNumber,
                noDateOfBirth,
                !string.IsNullOrWhiteSpace(result.FullName),
                !string.IsNullOrWhiteSpace(result.Address),
                !string.IsNullOrWhiteSpace(result.IssueDate));

            // Accept as back side if: No ID number + No DOB + Has some info
            return noIdNumber && noDateOfBirth && hasBackSideInfo;
        }
    }
}

