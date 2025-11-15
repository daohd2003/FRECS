using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.ProviderApplicationRepositories;
using Repositories.UserRepositories;
using Services.CloudServices;
using Services.EmailServices;
using Services.AI;

namespace Services.ProviderApplicationServices
{
    public class ProviderApplicationService : IProviderApplicationService
    {
        private readonly IProviderApplicationRepository _applicationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IEkycService _ekycService;
        private readonly IFaceMatchService _faceMatchService;

        public ProviderApplicationService(
            IProviderApplicationRepository applicationRepository,
            IUserRepository userRepository,
            IEmailService emailService,
            ICloudinaryService cloudinaryService,
            IEkycService ekycService,
            IFaceMatchService faceMatchService)
        {
            _applicationRepository = applicationRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _cloudinaryService = cloudinaryService;
            _ekycService = ekycService;
            _faceMatchService = faceMatchService;
        }

        public async Task<ProviderApplication> ApplyAsync(Guid userId, ProviderApplicationCreateDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId) ?? throw new InvalidOperationException("User not found");

            if (user.Role == UserRole.provider)
            {
                throw new InvalidOperationException("User is already a provider");
            }

            var existingPending = await _applicationRepository.GetPendingByUserIdAsync(userId);
            if (existingPending != null)
            {
                return existingPending;
            }

            // Validate Privacy Policy Agreement
            if (!dto.PrivacyPolicyAgreed)
            {
                throw new InvalidOperationException("You must agree to the privacy policy to continue.");
            }

            var app = new ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = dto.BusinessName,
                TaxId = dto.TaxId,
                ContactPhone = dto.ContactPhone,
                Notes = dto.Notes,
                ProviderType = dto.ProviderType,
                PrivacyPolicyAgreed = dto.PrivacyPolicyAgreed,
                PrivacyPolicyAgreedAt = DateTimeHelper.GetVietnamTime(),
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTimeHelper.GetVietnamTime()
            };

            // Validate Tax ID format
            var taxId = dto.TaxId?.Trim();
            if (string.IsNullOrEmpty(taxId) || !taxId.All(char.IsDigit))
            {
                throw new InvalidOperationException("Invalid Tax ID. Please enter 10 digits (business) or 12 digits (individual).");
            }

            // Determine provider type based on Tax ID length
            var isIndividual = taxId.Length == 12;
            var isBusiness = taxId.Length == 10;

            if (!isIndividual && !isBusiness)
            {
                throw new InvalidOperationException("Tax ID must be 10 digits (business) or 12 digits (individual).");
            }

            // Validate required images based on provider type
            if (dto.IdCardFrontImage == null || dto.IdCardBackImage == null)
            {
                throw new InvalidOperationException("Please provide both front and back ID card images.");
            }

            if (isIndividual && dto.SelfieImage == null)
            {
                throw new InvalidOperationException("Individual providers must provide a selfie image for face verification.");
            }

            if (isBusiness && dto.BusinessLicenseImage == null)
            {
                throw new InvalidOperationException("Business providers must provide a business license image.");
            }

            // === PROCESSING LOGIC ===
            if (isIndividual)
            {
                // Individual - require both ID card images
                if (dto.IdCardFrontImage == null || dto.IdCardBackImage == null)
                {
                    throw new InvalidOperationException("Please provide both front and back ID card images.");
                }

                // === BƯỚC 1: VERIFY CCCD bằng FPT.AI trước khi upload ===
                var verificationResult = await _ekycService.VerifyCccdBothSidesAsync(
                    dto.IdCardFrontImage,
                    dto.IdCardBackImage);

                // Lưu kết quả verification
                app.CccdVerified = verificationResult.IsValid;
                app.CccdIdNumber = verificationResult.IdNumber;
                app.CccdFullName = verificationResult.FullName;
                app.CccdDateOfBirth = verificationResult.DateOfBirth;
                app.CccdSex = verificationResult.Sex;
                app.CccdAddress = verificationResult.Address;
                app.CccdConfidenceScore = verificationResult.Confidence;
                app.CccdVerifiedAt = DateTimeHelper.GetVietnamTime();
                app.CccdVerificationError = verificationResult.ErrorMessage;

                // === BƯỚC 2: Kiểm tra xem CCCD có hợp lệ không ===
                if (!verificationResult.IsValid)
                {
                    throw new InvalidOperationException(
                        $"CCCD verification failed: {verificationResult.ErrorMessage ?? "Unable to verify ID card. Please ensure images are clear and of good quality."}");
                }

                // === BƯỚC 3: So sánh Tax ID với số CCCD (CHỈ CHO CÁ NHÂN) ===
                // ⚠️ IMPORTANT: Chỉ check match cho Individual (12 số)
                // Business (10 số) không cần check vì Tax ID là mã doanh nghiệp, không phải số CCCD
                var cccdNumber = verificationResult.IdNumber?.Replace(" ", "").Trim();
                var inputTaxId = taxId.Replace(" ", "").Trim();

                if (!string.IsNullOrEmpty(cccdNumber) && cccdNumber != inputTaxId)
                {
                    throw new InvalidOperationException(
                        $"Personal Tax ID ({inputTaxId}) does not match ID card number ({cccdNumber}). Please verify your information.");
                }

                // === BƯỚC 4: Kiểm tra confidence score (ít nhất 60%) ===
                // Production threshold: 60% for reliable CCCD verification
                if (verificationResult.Confidence < 0.60)
                {
                    throw new InvalidOperationException(
                        $"CCCD verification confidence too low ({verificationResult.Confidence:P}). " +
                        $"Required: ≥60%. Please upload clearer images with good lighting.");
                }

                // === BƯỚC 5: LIVENESS DETECTION (Verify người thật) ===
                // SKIPPED: Liveness bị bỏ qua để đơn giản hóa flow
                // Chỉ dùng CCCD + Face Matching (bảo mật ~80%)

                // === BƯỚC 6: FACE MATCHING (So khớp selfie với CCCD) ===
                if (dto.SelfieImage == null || dto.SelfieImage.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Selfie image is required for face matching verification. Please capture a clear photo of your face.");
                }

                var faceMatchResult = await _faceMatchService.CompareFacesAsync(
                    dto.SelfieImage,
                    dto.IdCardFrontImage); // Compare with front image of CCCD

                // Lưu kết quả face matching
                app.FaceMatched = faceMatchResult.IsMatched;
                app.FaceMatchScore = faceMatchResult.MatchScore;

                if (!faceMatchResult.IsMatched)
                {
                    throw new InvalidOperationException(
                        $"Face matching failed: {faceMatchResult.ErrorMessage ?? "Your face does not match the CCCD photo. Please ensure you're using your own ID card and take a clear selfie."}");
                }

                // Kiểm tra match score (ít nhất 70% - Production)
                // Production threshold: 70% for reliable face matching
                if (faceMatchResult.MatchScore < 0.70)
                {
                    throw new InvalidOperationException(
                        $"Face matching confidence too low ({faceMatchResult.MatchScore:P}). " +
                        $"Required: ≥70%. " +
                        $"Tips: Upload clearer CCCD photo, take selfie with similar lighting/angle.");
                }

                // === BƯỚC 7: Upload tất cả files lên Cloudinary (sau khi verify thành công) ===
                BusinessObject.DTOs.ProductDto.ImageUploadResult? frontImageResult = null;
                BusinessObject.DTOs.ProductDto.ImageUploadResult? backImageResult = null;
                BusinessObject.DTOs.ProductDto.ImageUploadResult? livenessVideoResult = null;
                BusinessObject.DTOs.ProductDto.ImageUploadResult? selfieResult = null;

                try
                {
                    // Upload CCCD mặt trước (PRIVATE)
                    frontImageResult = await _cloudinaryService.UploadPrivateImageAsync(
                        dto.IdCardFrontImage,
                        userId,
                        "ShareIt",
                        "provider-verification");

                    // Upload CCCD mặt sau (PRIVATE)
                    backImageResult = await _cloudinaryService.UploadPrivateImageAsync(
                        dto.IdCardBackImage,
                        userId,
                        "ShareIt",
                        "provider-verification");

                    // Upload Liveness video (SKIPPED - Not using Liveness Detection for now)
                    // Liveness bị bỏ qua để đơn giản hóa flow và cải thiện UX
                    // if (dto.LivenessVideo != null)
                    // {
                    //     livenessVideoResult = await _cloudinaryService.UploadSingleImageAsync(
                    //         dto.LivenessVideo, 
                    //         userId, 
                    //         "ShareIt", 
                    //         "provider-liveness");
                    //     app.LivenessVideoUrl = livenessVideoResult.ImageUrl;
                    // }

                    // Upload Selfie image (REQUIRED for Face Matching) (PRIVATE)
                    if (dto.SelfieImage != null)
                    {
                        selfieResult = await _cloudinaryService.UploadPrivateImageAsync(
                            dto.SelfieImage,
                            userId,
                            "ShareIt",
                            "provider-selfie");
                        app.SelfieImageUrl = selfieResult.ImageUrl;
                    }

                    // Lưu URLs
                    app.IdCardFrontImageUrl = frontImageResult.ImageUrl;
                    app.IdCardBackImageUrl = backImageResult.ImageUrl;
                }
                catch (Exception ex)
                {
                    // Clean up uploaded files if any error occurred
                    if (frontImageResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(frontImageResult.PublicId);
                    }
                    if (backImageResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(backImageResult.PublicId);
                    }
                    if (livenessVideoResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(livenessVideoResult.PublicId);
                    }
                    if (selfieResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(selfieResult.PublicId);
                    }
                    throw new InvalidOperationException($"An error occurred while uploading files. Please try again. Details: {ex.Message}");
                }
            }
            else if (isBusiness)
            {
                // === BUSINESS PROVIDER (10-digit Tax ID) ===
                // ⚠️ IMPORTANT: Tax ID (10 số) là mã số thuế DOANH NGHIỆP
                // CCCD là của người đại diện → KHÔNG CẦN check Tax ID match với CCCD
                Console.WriteLine($"[PROVIDER APPLICATION] Processing BUSINESS application with Tax ID: {taxId}");

                // === BƯỚC 1: VERIFY CCCD của người đại diện ===
                var verificationResult = await _ekycService.VerifyCccdBothSidesAsync(
                    dto.IdCardFrontImage,
                    dto.IdCardBackImage);

                // Lưu kết quả verification
                app.CccdVerified = verificationResult.IsValid;
                app.CccdIdNumber = verificationResult.IdNumber;
                app.CccdFullName = verificationResult.FullName;
                app.CccdDateOfBirth = verificationResult.DateOfBirth;
                app.CccdSex = verificationResult.Sex;
                app.CccdAddress = verificationResult.Address;
                app.CccdConfidenceScore = verificationResult.Confidence;
                app.CccdVerifiedAt = DateTimeHelper.GetVietnamTime();
                app.CccdVerificationError = verificationResult.ErrorMessage;

                // === BƯỚC 2: Kiểm tra xem CCCD có hợp lệ không ===
                if (!verificationResult.IsValid)
                {
                    throw new InvalidOperationException(
                        $"ID card verification failed: {verificationResult.ErrorMessage ?? "Unable to verify ID card. Please ensure images are clear and of good quality."}");
                }

                // === BƯỚC 3: Kiểm tra confidence score (ít nhất 60%) ===
                // Production threshold: 60% for reliable CCCD verification
                if (verificationResult.Confidence < 0.60)
                {
                    throw new InvalidOperationException(
                        $"CCCD verification confidence too low ({verificationResult.Confidence:P}). " +
                        $"Required: ≥60%. Please upload clearer images with good lighting.");
                }

                // === BƯỚC 4: Upload files lên Cloudinary ===
                BusinessObject.DTOs.ProductDto.ImageUploadResult? frontImageResult = null;
                BusinessObject.DTOs.ProductDto.ImageUploadResult? backImageResult = null;
                BusinessObject.DTOs.ProductDto.ImageUploadResult? businessLicenseResult = null;

                try
                {
                    // Upload CCCD mặt trước (PRIVATE)
                    frontImageResult = await _cloudinaryService.UploadPrivateImageAsync(
                        dto.IdCardFrontImage,
                        userId,
                        "ShareIt",
                        "business-verification");

                    // Upload CCCD mặt sau (PRIVATE)
                    backImageResult = await _cloudinaryService.UploadPrivateImageAsync(
                        dto.IdCardBackImage,
                        userId,
                        "ShareIt",
                        "business-verification");

                    // Upload Business License (REQUIRED for Business) (PRIVATE)
                    if (dto.BusinessLicenseImage != null)
                    {
                        businessLicenseResult = await _cloudinaryService.UploadPrivateImageAsync(
                            dto.BusinessLicenseImage,
                            userId,
                            "ShareIt",
                            "business-license");
                        app.BusinessLicenseImageUrl = businessLicenseResult.ImageUrl;
                    }

                    // Lưu URLs
                    app.IdCardFrontImageUrl = frontImageResult.ImageUrl;
                    app.IdCardBackImageUrl = backImageResult.ImageUrl;

                    Console.WriteLine($"[PROVIDER APPLICATION] Business files uploaded successfully");
                }
                catch (Exception ex)
                {
                    // Clean up uploaded files if any error occurred
                    if (frontImageResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(frontImageResult.PublicId);
                    }
                    if (backImageResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(backImageResult.PublicId);
                    }
                    if (businessLicenseResult != null)
                    {
                        await _cloudinaryService.DeleteImageAsync(businessLicenseResult.PublicId);
                    }
                    throw new InvalidOperationException($"An error occurred while uploading files. Please try again. Details: {ex.Message}");
                }
            }

            await _applicationRepository.AddAsync(app);
            return app;
        }

        public async Task<bool> ReviewAsync(Guid adminId, ProviderApplicationReviewDto dto)
        {
            var app = await _applicationRepository.GetByIdAsync(dto.ApplicationId);
            if (app == null) return false;

            if (app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            app.Status = dto.NewStatus;
            app.ReviewedAt = DateTime.UtcNow;
            app.ReviewedByAdminId = adminId;
            app.ReviewComment = dto.ReviewComment;

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            if (dto.NewStatus == ProviderApplicationStatus.approved)
            {
                var user = await _userRepository.GetByIdAsync(app.UserId);
                if (user == null) return false;
                user.Role = UserRole.provider;
                await _userRepository.UpdateAsync(user);
            }

            return true;
        }

        public Task<ProviderApplication?> GetMyPendingAsync(Guid userId)
        {
            return _applicationRepository.GetPendingByUserIdAsync(userId);
        }

        public Task<IEnumerable<ProviderApplication>> GetByStatusAsync(ProviderApplicationStatus status)
        {
            return _applicationRepository.GetByStatusAsync(status);
        }

        public async Task<IEnumerable<ProviderApplication>> GetAllApplicationsAsync(ProviderApplicationStatus? status)
        {
            if (status.HasValue)
            {
                return await _applicationRepository.GetByStatusAsync(status.Value);
            }
            return await _applicationRepository.GetAllWithUserDetailsAsync();
        }

        public async Task<bool> ApproveAsync(Guid staffId, Guid applicationId)
        {
            var app = await _applicationRepository.GetByIdAsync(applicationId);
            if (app == null || app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            // Update application status
            app.Status = ProviderApplicationStatus.approved;
            app.ReviewedAt = DateTimeHelper.GetVietnamTime();
            app.ReviewedByAdminId = staffId;
            app.ReviewComment = "Application approved";

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            // Update user role to provider
            var user = await _userRepository.GetByIdAsync(app.UserId);
            if (user == null) return false;

            user.Role = UserRole.provider;
            await _userRepository.UpdateAsync(user);

            // Send approval email
            try
            {
                await _emailService.SendProviderApplicationApprovedEmailAsync(user.Email, app.BusinessName);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Failed to send approval email: {ex.Message}");
            }

            return true;
        }

        public async Task<bool> RejectAsync(Guid staffId, Guid applicationId, string rejectionReason)
        {
            var app = await _applicationRepository.GetByIdAsync(applicationId);
            if (app == null || app.Status != ProviderApplicationStatus.pending)
            {
                return false;
            }

            // Update application status
            app.Status = ProviderApplicationStatus.rejected;
            app.ReviewedAt = DateTimeHelper.GetVietnamTime();
            app.ReviewedByAdminId = staffId;
            app.ReviewComment = rejectionReason;

            var updated = await _applicationRepository.UpdateAsync(app);
            if (!updated) return false;

            // Get user for email
            var user = await _userRepository.GetByIdAsync(app.UserId);
            if (user == null) return false;

            // Send rejection email
            try
            {
                await _emailService.SendProviderApplicationRejectedEmailAsync(
                    user.Email,
                    app.BusinessName,
                    rejectionReason);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Failed to send rejection email: {ex.Message}");
            }

            return true;
        }

        // Get application images with signed URLs for private access (Admin and Staff)
        public async Task<Dictionary<string, string>> GetApplicationImagesWithSignedUrlsAsync(Guid applicationId, Guid requesterId)
        {
            var app = await _applicationRepository.GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new Exception("Application not found");
            }

            var signedUrls = new Dictionary<string, string>();

            // Extract publicId from URL and generate signed URL
            if (!string.IsNullOrEmpty(app.IdCardFrontImageUrl))
            {
                var publicId = ExtractPublicIdFromUrl(app.IdCardFrontImageUrl);
                signedUrls["idCardFront"] = _cloudinaryService.GenerateSignedUrl(publicId, 60);
            }

            if (!string.IsNullOrEmpty(app.IdCardBackImageUrl))
            {
                var publicId = ExtractPublicIdFromUrl(app.IdCardBackImageUrl);
                signedUrls["idCardBack"] = _cloudinaryService.GenerateSignedUrl(publicId, 60);
            }

            if (!string.IsNullOrEmpty(app.SelfieImageUrl))
            {
                var publicId = ExtractPublicIdFromUrl(app.SelfieImageUrl);
                signedUrls["selfie"] = _cloudinaryService.GenerateSignedUrl(publicId, 60);
            }

            if (!string.IsNullOrEmpty(app.BusinessLicenseImageUrl))
            {
                var publicId = ExtractPublicIdFromUrl(app.BusinessLicenseImageUrl);
                signedUrls["businessLicense"] = _cloudinaryService.GenerateSignedUrl(publicId, 60);
            }

            return signedUrls;
        }

        // Helper method to extract publicId from Cloudinary URL
        private string ExtractPublicIdFromUrl(string url)
        {
            // Example URL: https://res.cloudinary.com/xxx/image/private/v123456/ShareIt/folder/publicId.jpg
            // We need: ShareIt/folder/publicId

            var uri = new Uri(url);
            var pathParts = uri.AbsolutePath.Split('/');

            // Find the version part (starts with 'v')
            int versionIndex = -1;
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i].StartsWith("v") && pathParts[i].Length > 1 && char.IsDigit(pathParts[i][1]))
                {
                    versionIndex = i;
                    break;
                }
            }

            if (versionIndex == -1 || versionIndex >= pathParts.Length - 1)
            {
                throw new Exception("Invalid Cloudinary URL format");
            }

            // Get everything after version, remove file extension
            var publicIdParts = pathParts.Skip(versionIndex + 1).ToArray();
            var publicIdWithExt = string.Join("/", publicIdParts);

            // Remove extension
            var lastDotIndex = publicIdWithExt.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return publicIdWithExt.Substring(0, lastDotIndex);
            }

            return publicIdWithExt;
        }
    }
}


