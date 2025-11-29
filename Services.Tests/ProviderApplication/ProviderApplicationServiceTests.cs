using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.ProviderApplicationRepositories;
using Repositories.UserRepositories;
using Services.CloudServices;
using Services.EmailServices;
using Services.ProviderApplicationServices;
using Services.AI;

namespace Services.Tests.ProviderApplicationTests
{
    /// <summary>
    /// Unit tests for ProviderApplicationService.ApplyAsync method
    /// Tests the Register to become provider functionality
    /// 
    /// Note: Based on current DTO implementation:
    ///  - BusinessName is [Required]
    ///  - TaxId, ContactPhone, Notes are all OPTIONAL (nullable)
    /// 
    /// Validation messages from DTO:
    ///  - "The BusinessName field is required." (when blank)
    ///  - "The field BusinessName must be a string with a maximum length of 255." (when > 255 chars)
    /// </summary>
    public class ProviderApplicationServiceTests
    {
        private readonly Mock<IProviderApplicationRepository> _mockApplicationRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<IEkycService> _mockEkycService;
        private readonly Mock<IFaceMatchService> _mockFaceMatchService;
        private readonly ProviderApplicationService _service;

        public ProviderApplicationServiceTests()
        {
            _mockApplicationRepository = new Mock<IProviderApplicationRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockEkycService = new Mock<IEkycService>();
            _mockFaceMatchService = new Mock<IFaceMatchService>();
            _service = new ProviderApplicationService(
                _mockApplicationRepository.Object,
                _mockUserRepository.Object,
                _mockEmailService.Object,
                _mockCloudinaryService.Object,
                _mockEkycService.Object,
                _mockFaceMatchService.Object
            );
        }

        private void SetupMocksForSuccessfulBusinessApplication()
        {
            // Mock Cloudinary uploads
            _mockCloudinaryService.Setup(x => x.UploadPrivateImageAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ImageUploadResult
                {
                    ImageUrl = "https://cloudinary.com/test.jpg",
                    PublicId = "test_id"
                });

            // Mock eKYC verification for Business provider (representative's CCCD)
            _mockEkycService.Setup(x => x.VerifyCccdBothSidesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
                .ReturnsAsync(new BusinessObject.DTOs.AIDtos.CccdVerificationResultDto
                {
                    IsValid = true,
                    IdNumber = "001234567890",
                    FullName = "Test Representative",
                    DateOfBirth = "01/01/1990",
                    Sex = "Male",
                    Address = "Test Address",
                    Confidence = 0.95, // >= 60% required
                    ErrorMessage = null
                });
        }

        private void SetupMocksForSuccessfulIndividualApplication(string cccdNumber = "123456789012")
        {
            // Mock Cloudinary uploads
            _mockCloudinaryService.Setup(x => x.UploadPrivateImageAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ImageUploadResult
                {
                    ImageUrl = "https://cloudinary.com/test.jpg",
                    PublicId = "test_id"
                });

            // Mock eKYC verification for Individual provider
            _mockEkycService.Setup(x => x.VerifyCccdBothSidesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
                .ReturnsAsync(new BusinessObject.DTOs.AIDtos.CccdVerificationResultDto
                {
                    IsValid = true,
                    IdNumber = cccdNumber,
                    FullName = "Test Individual",
                    DateOfBirth = "01/01/1990",
                    Sex = "Male",
                    Address = "Test Address",
                    Confidence = 0.95, // >= 60% required
                    ErrorMessage = null
                });

            // Mock Face Matching for Individual provider
            _mockFaceMatchService.Setup(x => x.CompareFacesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
                .ReturnsAsync(new BusinessObject.DTOs.AIDtos.FaceMatchResultDto
                {
                    IsMatched = true,
                    MatchScore = 0.85, // >= 70% required
                    ErrorMessage = null
                });
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Apply as Business provider with all valid fields
        /// Expected: Application created successfully with CCCD verification
        /// Business provider (10-digit Tax ID):
        ///   - Requires: IdCardFront, IdCardBack, BusinessLicense
        ///   - CCCD verification required (>= 60% confidence)
        ///   - NO Face Matching required
        ///   - Tax ID is business registration number (not CCCD number)
        /// </summary>
        [Fact]
        public async Task UTCID01_Apply_BusinessProvider_AllValidFields_ShouldCreateSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business Corp",
                TaxId = "1234567890", // 10 digits = Business
                ContactPhone = "0123456789",
                Notes = "I want to become a provider",
                ProviderType = "Business",
                IdCardFrontImage = CreateMockFormFile("front.jpg"),
                IdCardBackImage = CreateMockFormFile("back.jpg"),
                BusinessLicenseImage = CreateMockFormFile("license.jpg"),
                PrivacyPolicyAgreed = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication?)null);

            SetupMocksForSuccessfulBusinessApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(dto.BusinessName, result.BusinessName);
            Assert.Equal(dto.TaxId, result.TaxId);
            Assert.Equal(dto.ContactPhone, result.ContactPhone);
            Assert.Equal(dto.Notes, result.Notes);
            Assert.Equal(ProviderApplicationStatus.pending, result.Status);
            Assert.True(result.PrivacyPolicyAgreed);
            Assert.NotNull(result.PrivacyPolicyAgreedAt);
            
            // Verify CCCD verification was performed
            Assert.True(result.CccdVerified);
            Assert.NotNull(result.CccdIdNumber);
            Assert.NotNull(result.CccdFullName);
            Assert.True(result.CccdConfidenceScore >= 0.60);

            _mockApplicationRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.ProviderApplication>(
                app => app.UserId == userId &&
                       app.BusinessName == dto.BusinessName &&
                       app.TaxId == dto.TaxId &&
                       app.Status == ProviderApplicationStatus.pending &&
                       app.CccdVerified == true
            )), Times.Once);
            
            // Verify eKYC was called
            _mockEkycService.Verify(x => x.VerifyCccdBothSidesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Once);
            
            // Verify Face Matching was NOT called for Business
            _mockFaceMatchService.Verify(x => x.CompareFacesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Never);
        }

        private Microsoft.AspNetCore.Http.IFormFile CreateMockFormFile(string fileName)
        {
            var content = "fake image content";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var file = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            file.Setup(f => f.FileName).Returns(fileName);
            file.Setup(f => f.Length).Returns(bytes.Length);
            // Return new stream each time to avoid stream closed issues
            file.Setup(f => f.OpenReadStream()).Returns(() => new System.IO.MemoryStream(bytes));
            file.Setup(f => f.ContentType).Returns("image/jpeg");
            file.Setup(f => f.CopyToAsync(It.IsAny<System.IO.Stream>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns((System.IO.Stream target, System.Threading.CancellationToken token) =>
                {
                    var ms = new System.IO.MemoryStream(bytes);
                    return ms.CopyToAsync(target, token);
                });
            return file.Object;
        }

        /// <summary>
        /// UTCID02: Apply as Individual provider with all valid fields
        /// Expected: Application created successfully with CCCD verification AND Face Matching
        /// Individual provider (12-digit Tax ID = CCCD number):
        ///   - Requires: IdCardFront, IdCardBack, Selfie
        ///   - CCCD verification required (>= 60% confidence)
        ///   - Face Matching required (>= 70% match score)
        ///   - Tax ID must match CCCD number
        /// </summary>
        [Fact]
        public async Task UTCID02_Apply_IndividualProvider_AllValidFields_ShouldCreateSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cccdNumber = "123456789012"; // 12 digits
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Individual Provider",
                TaxId = cccdNumber, // 12 digits = Individual (must match CCCD)
                ContactPhone = "0123456789",
                Notes = "I want to become an individual provider",
                ProviderType = "Individual",
                IdCardFrontImage = CreateMockFormFile("front.jpg"),
                IdCardBackImage = CreateMockFormFile("back.jpg"),
                SelfieImage = CreateMockFormFile("selfie.jpg"),
                PrivacyPolicyAgreed = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication?)null);

            SetupMocksForSuccessfulIndividualApplication(cccdNumber);

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(dto.BusinessName, result.BusinessName);
            Assert.Equal(dto.TaxId, result.TaxId);
            Assert.Equal(ProviderApplicationStatus.pending, result.Status);
            
            // Verify CCCD verification
            Assert.True(result.CccdVerified);
            Assert.Equal(cccdNumber, result.CccdIdNumber);
            Assert.True(result.CccdConfidenceScore >= 0.60);
            
            // Verify Face Matching
            Assert.True(result.FaceMatched);
            Assert.True(result.FaceMatchScore >= 0.70);

            // Verify eKYC was called
            _mockEkycService.Verify(x => x.VerifyCccdBothSidesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Once);
            
            // Verify Face Matching was called for Individual
            _mockFaceMatchService.Verify(x => x.CompareFacesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()), Times.Once);
        }

        /// <summary>
        /// UTCID03: Apply with Notes = null (optional field)
        /// Expected: Application created successfully
        /// </summary>
        [Fact]
        public async Task UTCID03_Apply_OptionalNotesNull_ShouldCreateSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business",
                TaxId = "1234567890", // 10 digits = Business
                ContactPhone = "0123456789",
                Notes = null, // Optional field
                ProviderType = "Business",
                IdCardFrontImage = CreateMockFormFile("front.jpg"),
                IdCardBackImage = CreateMockFormFile("back.jpg"),
                BusinessLicenseImage = CreateMockFormFile("license.jpg"),
                PrivacyPolicyAgreed = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication?)null);

            SetupMocksForSuccessfulBusinessApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.Notes);
            Assert.Equal(ProviderApplicationStatus.pending, result.Status);
        }

        #endregion

        #region Validation Test Cases

        /// <summary>
        /// UTCID04: Apply without agreeing to Privacy Policy
   

        /// <summary>
        /// Additional Test: User not found should throw InvalidOperationException
        /// </summary>
        [Fact]
        public async Task Additional_Apply_UserNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.ApplyAsync(userId, dto)
            );

            Assert.Equal("User not found", exception.Message);

            _mockApplicationRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: User already a provider should throw InvalidOperationException
        /// </summary>
        [Fact]
        public async Task Additional_Apply_UserAlreadyProvider_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "provider@example.com",
                Role = UserRole.provider, // Already a provider
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.ApplyAsync(userId, dto)
            );

            Assert.Equal("User is already a provider", exception.Message);

            _mockApplicationRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: User has existing pending application should return existing one
        /// </summary>
        [Fact]
        public async Task Additional_Apply_ExistingPendingApplication_ShouldReturnExisting()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var existingApplication = new BusinessObject.Models.ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = "Existing Business",
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "New Business"
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync(existingApplication);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingApplication.Id, result.Id);
            Assert.Equal(existingApplication.BusinessName, result.BusinessName); // Should return existing, not new
            Assert.Equal(ProviderApplicationStatus.pending, result.Status);

            // Should NOT create new application
            _mockApplicationRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Verify CreatedAt is set
        /// </summary>
        [Fact]
        public async Task Additional_Apply_ShouldSetCreatedAt()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business",
                TaxId = "1234567890",
                ContactPhone = "0123456789",
                ProviderType = "Business",
                IdCardFrontImage = CreateMockFormFile("front.jpg"),
                IdCardBackImage = CreateMockFormFile("back.jpg"),
                BusinessLicenseImage = CreateMockFormFile("license.jpg"),
                PrivacyPolicyAgreed = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication?)null);

            SetupMocksForSuccessfulBusinessApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(default(DateTime), result.CreatedAt);
        }

        /// <summary>
        /// Additional Test: Verify unique ID is generated
        /// </summary>
        [Fact]
        public async Task Additional_Apply_ShouldGenerateUniqueId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "customer@example.com",
                Role = UserRole.customer,
                IsActive = true,
                EmailConfirmed = true
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business",
                TaxId = "1234567890",
                ContactPhone = "0123456789",
                ProviderType = "Business",
                IdCardFrontImage = CreateMockFormFile("front.jpg"),
                IdCardBackImage = CreateMockFormFile("back.jpg"),
                BusinessLicenseImage = CreateMockFormFile("license.jpg"),
                PrivacyPolicyAgreed = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockApplicationRepository.Setup(x => x.GetPendingByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication?)null);

            SetupMocksForSuccessfulBusinessApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
        }

        #endregion

        #region View Provider Applications Tests

        /// <summary>
        /// Test: Get all applications without filter
        /// Expected: Returns all applications
        /// </summary>
        [Fact]
        public async Task GetAllApplications_NoFilter_ShouldReturnAllApplications()
        {
            // Arrange
            var applications = new List<BusinessObject.Models.ProviderApplication>
            {
                new BusinessObject.Models.ProviderApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    BusinessName = "Test Business 1",
                    Status = ProviderApplicationStatus.pending
                },
                new BusinessObject.Models.ProviderApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    BusinessName = "Test Business 2",
                    Status = ProviderApplicationStatus.approved
                }
            };

            _mockApplicationRepository.Setup(x => x.GetAllWithUserDetailsAsync())
                .ReturnsAsync(applications);

            // Act
            var result = await _service.GetAllApplicationsAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            _mockApplicationRepository.Verify(x => x.GetAllWithUserDetailsAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get applications filtered by status
        /// Expected: Returns only applications with specified status
        /// </summary>
        [Fact]
        public async Task GetAllApplications_WithStatusFilter_ShouldReturnFilteredApplications()
        {
            // Arrange
            var pendingApplications = new List<BusinessObject.Models.ProviderApplication>
            {
                new BusinessObject.Models.ProviderApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    BusinessName = "Pending Business",
                    Status = ProviderApplicationStatus.pending
                }
            };

            _mockApplicationRepository.Setup(x => x.GetByStatusAsync(ProviderApplicationStatus.pending))
                .ReturnsAsync(pendingApplications);

            // Act
            var result = await _service.GetAllApplicationsAsync(ProviderApplicationStatus.pending);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.All(result, app => Assert.Equal(ProviderApplicationStatus.pending, app.Status));
            _mockApplicationRepository.Verify(x => x.GetByStatusAsync(ProviderApplicationStatus.pending), Times.Once);
        }

        /// <summary>
        /// Test: Get applications when none exist
        /// Expected: Returns empty list
        /// </summary>
        [Fact]
        public async Task GetAllApplications_NoApplications_ShouldReturnEmptyList()
        {
            // Arrange
            _mockApplicationRepository.Setup(x => x.GetAllWithUserDetailsAsync())
                .ReturnsAsync(new List<BusinessObject.Models.ProviderApplication>());

            // Act
            var result = await _service.GetAllApplicationsAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockApplicationRepository.Verify(x => x.GetAllWithUserDetailsAsync(), Times.Once);
        }

        #endregion

        #region Approve Provider Application Tests

        /// <summary>
        /// Test: Approve a pending application
        /// Expected: Application status changes to approved, user role changes to provider
        /// Message: "Application approved" (set in ReviewComment)
        /// </summary>
        [Fact]
        public async Task Approve_PendingApplication_ShouldApproveAndChangeUserRole()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = userId,
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.pending
            };

            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                Role = UserRole.customer
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);
            _mockApplicationRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .ReturnsAsync(true);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);
            _mockEmailService.Setup(x => x.SendProviderApplicationApprovedEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApproveAsync(staffId, applicationId);

            // Assert
            Assert.True(result);
            Assert.Equal(ProviderApplicationStatus.approved, application.Status);
            Assert.Equal("Application approved", application.ReviewComment);
            Assert.NotNull(application.ReviewedAt);
            Assert.Equal(staffId, application.ReviewedByAdminId);
            Assert.Equal(UserRole.provider, user.Role);

            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.ProviderApplication>(
                app => app.Status == ProviderApplicationStatus.approved
            )), Times.Once);
            _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<User>(
                u => u.Role == UserRole.provider
            )), Times.Once);
            _mockEmailService.Verify(x => x.SendProviderApplicationApprovedEmailAsync(user.Email, application.BusinessName), Times.Once);
        }

        /// <summary>
        /// Test: Approve non-existent application
        /// Expected: Returns false
        /// </summary>
        [Fact]
        public async Task Approve_NonExistentApplication_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication)null);

            // Act
            var result = await _service.ApproveAsync(staffId, applicationId);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        /// <summary>
        /// Test: Approve already approved application
        /// Expected: Returns false, no changes made
        /// </summary>
        [Fact]
        public async Task Approve_AlreadyApprovedApplication_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = Guid.NewGuid(),
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.approved
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);

            // Act
            var result = await _service.ApproveAsync(staffId, applicationId);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Test: Approve application when user not found
        /// Expected: Returns false after updating application
        /// </summary>
        [Fact]
        public async Task Approve_UserNotFound_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = userId,
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.pending
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);
            _mockApplicationRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .ReturnsAsync(true);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync((User)null);

            // Act
            var result = await _service.ApproveAsync(staffId, applicationId);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Once);
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        #endregion

        #region Reject Provider Application Tests

        /// <summary>
        /// Test: Reject a pending application with reason
        /// Expected: Application status changes to rejected, rejection reason saved
        /// </summary>
        [Fact]
        public async Task Reject_PendingApplication_ShouldRejectWithReason()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rejectionReason = "Incomplete documentation";

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = userId,
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.pending
            };

            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                Role = UserRole.customer
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);
            _mockApplicationRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .ReturnsAsync(true);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);
            _mockEmailService.Setup(x => x.SendProviderApplicationRejectedEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.RejectAsync(staffId, applicationId, rejectionReason);

            // Assert
            Assert.True(result);
            Assert.Equal(ProviderApplicationStatus.rejected, application.Status);
            Assert.Equal(rejectionReason, application.ReviewComment);
            Assert.NotNull(application.ReviewedAt);
            Assert.Equal(staffId, application.ReviewedByAdminId);
            Assert.Equal(UserRole.customer, user.Role); // Role should NOT change

            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.ProviderApplication>(
                app => app.Status == ProviderApplicationStatus.rejected &&
                       app.ReviewComment == rejectionReason
            )), Times.Once);
            _mockEmailService.Verify(x => x.SendProviderApplicationRejectedEmailAsync(
                user.Email, application.BusinessName, rejectionReason), Times.Once);
        }

        /// <summary>
        /// Test: Reject non-existent application
        /// Expected: Returns false
        /// </summary>
        [Fact]
        public async Task Reject_NonExistentApplication_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var rejectionReason = "Test reason";

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync((BusinessObject.Models.ProviderApplication)null);

            // Act
            var result = await _service.RejectAsync(staffId, applicationId, rejectionReason);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Test: Reject already rejected application
        /// Expected: Returns false, no changes made
        /// </summary>
        [Fact]
        public async Task Reject_AlreadyRejectedApplication_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var rejectionReason = "Test reason";

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = Guid.NewGuid(),
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.rejected
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);

            // Act
            var result = await _service.RejectAsync(staffId, applicationId, rejectionReason);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Never);
        }

        /// <summary>
        /// Test: Reject application when user not found
        /// Expected: Returns false after updating application
        /// </summary>
        [Fact]
        public async Task Reject_UserNotFound_ShouldReturnFalse()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rejectionReason = "Test reason";

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = userId,
                BusinessName = "Test Business",
                Status = ProviderApplicationStatus.pending
            };

            _mockApplicationRepository.Setup(x => x.GetByIdAsync(applicationId))
                .ReturnsAsync(application);
            _mockApplicationRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .ReturnsAsync(true);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync((User)null);

            // Act
            var result = await _service.RejectAsync(staffId, applicationId, rejectionReason);

            // Assert
            Assert.False(result);
            _mockApplicationRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Once);
        }

        #endregion
    }
}

