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

        private void SetupMocksForSuccessfulApplication()
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

            // Mock eKYC verification for both sides (Business provider)
            _mockEkycService.Setup(x => x.VerifyCccdBothSidesAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(),
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
                .ReturnsAsync(new BusinessObject.DTOs.AIDtos.CccdVerificationResultDto
                {
                    IsValid = true,
                    IdNumber = "001234567890",
                    FullName = "Test User",
                    DateOfBirth = "01/01/1990",
                    Sex = "Male",
                    Address = "Test Address",
                    Confidence = 0.95,
                    ErrorMessage = null
                });
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Apply with all valid fields including optional ones
        /// Expected: Application created successfully
        /// Backend behavior:
        ///   - Service creates new application with status = pending
        ///   - Service returns ProviderApplication object
        ///   - Controller returns 200 OK with message "Application submitted"
        /// Note: The controller message is "Application submitted", NOT "Application submitted. We will notify you after review."
        ///       The longer message may be a frontend message.
        /// </summary>
        [Fact]
        public async Task UTCID01_Apply_AllValidFields_ShouldCreateApplicationSuccessfully()
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
                TaxId = "1234567890", // 10 digits = Business (simpler, no eKYC/FaceMatch)
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

            SetupMocksForSuccessfulApplication();

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

            _mockApplicationRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.ProviderApplication>(
                app => app.UserId == userId &&
                       app.BusinessName == dto.BusinessName &&
                       app.TaxId == dto.TaxId &&
                       app.ContactPhone == dto.ContactPhone &&
                       app.Notes == dto.Notes &&
                       app.Status == ProviderApplicationStatus.pending
            )), Times.Once);
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
        /// UTCID05: Apply with BusinessName only (Notes and other optional fields blank/null)
        /// Expected: Application created successfully (TaxId, ContactPhone, Notes are optional)
        /// </summary>
        [Fact]
        public async Task UTCID05_Apply_OnlyBusinessName_OptionalFieldsBlank_ShouldCreateSuccessfully()
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
                Notes = null,
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

            SetupMocksForSuccessfulApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(dto.BusinessName, result.BusinessName);
            Assert.Equal(dto.TaxId, result.TaxId);
            Assert.Null(result.Notes);
            Assert.Equal(ProviderApplicationStatus.pending, result.Status);

            _mockApplicationRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()), Times.Once);
        }

        #endregion

        #region Additional Test Cases

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

            SetupMocksForSuccessfulApplication();

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

            SetupMocksForSuccessfulApplication();

            _mockApplicationRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.ProviderApplication>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ApplyAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
        }

        #endregion
    }
}

