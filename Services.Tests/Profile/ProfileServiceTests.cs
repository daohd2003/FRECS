using BusinessObject.Models;
using Moq;
using Repositories.ProfileRepositories;
using Services.ProfileServices;

namespace Services.Tests.ProfileTests
{
    /// <summary>
    /// Unit tests for View My Profile functionality (Service Layer)
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬────────────────────────────────────────────────┬──────────────────────────────────────┐
    /// │ Test ID │ Input                                          │ Expected Result                      │
    /// ├─────────┼────────────────────────────────────────────────┼──────────────────────────────────────┤
    /// │ UTCID01 │ Valid userId with corresponding profile       │ Return Profile object                │
    /// │ UTCID02 │ Valid userId without corresponding profile    │ Return null                          │
    /// └─────────┴────────────────────────────────────────────────┴──────────────────────────────────────┘
    /// 
    /// Note: Service layer returns Profile object or null.
    ///       Messages ("Profile retrieved successfully", "Profile not found") 
    ///       are returned by the Controller layer (tested separately in Controller tests).
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Run specific test: dotnet test --filter "FullyQualifiedName~ProfileServiceTests.UTCID01"
    /// 3. Run all profile service tests: dotnet test --filter "FullyQualifiedName~ProfileServiceTests"
    /// </summary>
    public class ProfileServiceTests
    {
        private readonly Mock<IProfileRepository> _mockProfileRepository;
        private readonly ProfileService _profileService;

        public ProfileServiceTests()
        {
            _mockProfileRepository = new Mock<IProfileRepository>();
            _profileService = new ProfileService(_mockProfileRepository.Object);
        }

        /// <summary>
        /// UTCID01: Valid userId that has a corresponding profile in the database
        /// Expected: Return Profile object
        /// Backend Service: Returns Profile object
        /// API Message (from Controller): "Profile retrieved successfully"
        /// </summary>
        [Fact]
        public async Task UTCID01_ValidUserIdWithProfile_ShouldReturnProfileObject()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedProfile = new BusinessObject.Models.Profile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FullName = "Test User",
                Phone = "0123456789",
                Address = "123 Test Street",
                ProfilePictureUrl = "https://example.com/avatar.jpg"
            };

            _mockProfileRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync(expectedProfile);

            // Act
            var result = await _profileService.GetByUserIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal("Test User", result.FullName);
            Assert.Equal("0123456789", result.Phone);
            Assert.Equal("123 Test Street", result.Address);
            Assert.Equal("https://example.com/avatar.jpg", result.ProfilePictureUrl);

            _mockProfileRepository.Verify(x => x.GetByUserIdAsync(userId), Times.Once);

            // API Controller returns: "Profile retrieved successfully" (when profile is not null)
        }

        /// <summary>
        /// UTCID02: Valid userId that does not have a corresponding profile
        /// Expected: Return null
        /// Backend Service: Returns null
        /// API Message (from Controller): "Profile not found"
        /// </summary>
        [Fact]
        public async Task UTCID02_ValidUserIdWithoutProfile_ShouldReturnNull()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockProfileRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.Profile?)null);

            // Act
            var result = await _profileService.GetByUserIdAsync(userId);

            // Assert
            Assert.Null(result);

            _mockProfileRepository.Verify(x => x.GetByUserIdAsync(userId), Times.Once);

            // API Controller returns: "Profile not found" (when profile is null)
        }

        /// <summary>
        /// Additional Test: Verify repository is called with correct userId
        /// </summary>
        [Fact]
        public async Task Additional_GetByUserIdAsync_ShouldCallRepositoryWithCorrectUserId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockProfileRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync((BusinessObject.Models.Profile?)null);

            // Act
            await _profileService.GetByUserIdAsync(userId);

            // Assert
            _mockProfileRepository.Verify(x => x.GetByUserIdAsync(It.Is<Guid>(id => id == userId)), Times.Once);
        }

        /// <summary>
        /// Additional Test: Verify profile with all fields populated
        /// </summary>
        [Fact]
        public async Task Additional_ProfileWithAllFields_ShouldReturnCompleteProfile()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new BusinessObject.Models.Profile
            {
                Id = profileId,
                UserId = userId,
                FullName = "John Doe",
                Phone = "0987654321",
                Address = "456 Main St, City, Country",
                ProfilePictureUrl = "https://example.com/john.jpg",
                User = new User
                {
                    Id = userId,
                    Email = "john@example.com"
                }
            };

            _mockProfileRepository.Setup(x => x.GetByUserIdAsync(userId))
                .ReturnsAsync(profile);

            // Act
            var result = await _profileService.GetByUserIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(profileId, result.Id);
            Assert.Equal(userId, result.UserId);
            Assert.Equal("John Doe", result.FullName);
            Assert.Equal("0987654321", result.Phone);
            Assert.Equal("456 Main St, City, Country", result.Address);
            Assert.Equal("https://example.com/john.jpg", result.ProfilePictureUrl);
            Assert.NotNull(result.User);
            Assert.Equal("john@example.com", result.User.Email);
        }

        #region Update Profile Tests

        /// <summary>
        /// Update Profile - Valid data
        /// Expected: UpdateAsync is called successfully
        /// </summary>
        [Fact]
        public async Task UpdateProfile_ValidData_ShouldCallRepositoryUpdate()
        {
            // Arrange
            var profile = new BusinessObject.Models.Profile
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                FullName = "Updated Name",
                Phone = "0123456789",
                Address = "Updated Address"
            };

            _mockProfileRepository.Setup(x => x.UpdateAsync(profile))
                .Returns(Task.CompletedTask);

            // Act
            await _profileService.UpdateAsync(profile);

            // Assert
            _mockProfileRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Profile>(p =>
                p.FullName == "Updated Name" &&
                p.Phone == "0123456789" &&
                p.Address == "Updated Address"
            )), Times.Once);
        }

        /// <summary>
        /// Update Profile - Verify all fields are updated
        /// </summary>
        [Fact]
        public async Task UpdateProfile_AllFields_ShouldUpdateAllProperties()
        {
            // Arrange
            var profile = new BusinessObject.Models.Profile
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                FullName = "New Full Name",
                Phone = "0987654321",
                Address = "123 New Street, New City"
            };

            BusinessObject.Models.Profile? capturedProfile = null;
            _mockProfileRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Profile>()))
                .Callback<BusinessObject.Models.Profile>(p => capturedProfile = p)
                .Returns(Task.CompletedTask);

            // Act
            await _profileService.UpdateAsync(profile);

            // Assert
            Assert.NotNull(capturedProfile);
            Assert.Equal("New Full Name", capturedProfile.FullName);
            Assert.Equal("0987654321", capturedProfile.Phone);
            Assert.Equal("123 New Street, New City", capturedProfile.Address);
        }

        #endregion
    }
}

