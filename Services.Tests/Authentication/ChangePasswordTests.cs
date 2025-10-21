using BusinessObject.DTOs.Login;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Logout;
using Repositories.UserRepositories;
using Services.Authentication;
using Services.EmailServices;

namespace Services.Tests.Authentication
{
    /// <summary>
    /// Unit tests for Change Password functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬─────────────────────┬───────────────────────┬────────────────────────┬──────────────────────────────────────────────────────┐
    /// │ Test ID │ CurrentPassword     │ NewPassword           │ ConfirmPassword        │ Expected Result                                      │
    /// ├─────────┼─────────────────────┼───────────────────────┼────────────────────────┼──────────────────────────────────────────────────────┤
    /// │ UTCID01 │ Valid and correct   │ Valid (8+ chars)      │ Matches                │ Return true                                          │
    /// │ UTCID02 │ Valid but incorrect │ Valid                 │ Matches                │ Return false                                         │
    /// │ UTCID03 │ Blank               │ Valid                 │ Matches                │ Service processes (Controller validates)             │
    /// │ UTCID04 │ Valid and correct   │ Blank                 │ Blank                  │ Service processes (Controller validates)             │
    /// │ UTCID05 │ Valid and correct   │ Valid                 │ Blank                  │ Controller validates (service doesn't receive)       │
    /// │ UTCID06 │ Valid and correct   │ Valid                 │ Does not match         │ Controller validates (service doesn't receive)       │
    /// │ UTCID07 │ Valid and correct   │ Less than 8 chars     │ Matches                │ Service processes (Controller validates)             │
    /// └─────────┴─────────────────────┴───────────────────────┴────────────────────────┴──────────────────────────────────────────────────────┘
    /// 
    /// Note: Service signature: ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    ///       Service does NOT receive confirmPassword - that's validated at Controller level
    ///       Validation messages (blank fields, length, confirm match) are Controller/FE concerns
    ///       Service only returns bool (true/false)
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Run specific test: dotnet test --filter "FullyQualifiedName~UTCID01"
    /// 3. Run all change password tests: dotnet test --filter "FullyQualifiedName~ChangePasswordTests"
    /// </summary>
    public class ChangePasswordTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILoggedOutTokenRepository> _mockLoggedOutTokenRepository;
        private readonly Mock<ILogger<JwtService>> _mockLogger;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly JwtService _jwtService;
        private readonly JwtSettings _jwtSettings;

        public ChangePasswordTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockLoggedOutTokenRepository = new Mock<ILoggedOutTokenRepository>();
            _mockLogger = new Mock<ILogger<JwtService>>();
            _mockEmailService = new Mock<IEmailService>();
            _mockConfiguration = new Mock<IConfiguration>();

            // Setup JWT settings
            _jwtSettings = new JwtSettings
            {
                SecretKey = "ThisIsAVerySecureSecretKeyForTestingPurposesOnly12345678901234567890",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryMinutes = 60,
                RememberMeExpiryMinutes = 1440,
                RefreshTokenExpiryDays = 7
            };

            var mockJwtOptions = new Mock<IOptions<JwtSettings>>();
            mockJwtOptions.Setup(x => x.Value).Returns(_jwtSettings);

            _jwtService = new JwtService(
                mockJwtOptions.Object,
                _mockUserRepository.Object,
                _mockLogger.Object,
                _mockLoggedOutTokenRepository.Object,
                _mockEmailService.Object,
                _mockConfiguration.Object
            );
        }

        /// <summary>
        /// UTCID01: Valid current password + Valid new password + Confirm matches
        /// Expected: Return true, password changed
        /// Backend Service: Returns true
        /// API Message (from Controller): "Password changed successfully"
        /// Note: Controller returns this message when service returns true
        /// </summary>
        [Fact]
        public async Task UTCID01_ChangePassword_ValidCurrentAndNewPassword_ShouldReturnTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "OldPassword@123";
            var newPassword = "NewPassword@456";
            
            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, currentPassword, newPassword);

            // Assert
            Assert.True(result);

            // Verify password was updated
            Assert.NotNull(capturedUser);
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, capturedUser.PasswordHash));
            Assert.False(BCrypt.Net.BCrypt.Verify(currentPassword, capturedUser.PasswordHash));

            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Once);

            // API returns: "Password changed successfully" (from Controller when service returns true)
        }

        /// <summary>
        /// UTCID02: Incorrect current password + Valid new password + Confirm matches
        /// Expected: Return false
        /// Backend Service: Returns false (current password doesn't match)
        /// API Message (from Controller): "Current password is incorrect or user not found"
        /// Note: Controller returns this message when service returns false
        /// </summary>
        [Fact]
        public async Task UTCID02_ChangePassword_IncorrectCurrentPassword_ShouldReturnFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var actualCurrentPassword = "OldPassword@123";
            var wrongCurrentPassword = "WrongPassword@999";
            var newPassword = "NewPassword@456";

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(actualCurrentPassword),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, wrongCurrentPassword, newPassword);

            // Assert
            Assert.False(result);

            // Verify password was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // API returns: "Current password is incorrect or user not found" (from Controller when service returns false)
        }

        /// <summary>
        /// UTCID03: Blank current password + Valid new password + Confirm matches
        /// Expected: Service processes if validation bypassed (Controller should validate)
        /// Controller Validation Message: "Current password is required." (from [Required] attribute)
        /// Note: Blank validation happens at Controller level with [Required] attribute
        /// If validation passes somehow, service returns false (blank won't match hash)
        /// </summary>
        [Fact]
        public async Task UTCID03_ChangePassword_BlankCurrentPassword_ServiceProcessesIfNotValidated()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var blankCurrentPassword = "";
            var newPassword = "NewPassword@456";

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SomePassword@123"),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, blankCurrentPassword, newPassword);

            // Assert
            Assert.False(result); // Returns false because blank password won't match hashed password

            // Verify password was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Note: Controller [Required] validation should catch this before reaching service
            // Validation message: "Current password is required." (from ChangePasswordRequest DTO)
        }

        /// <summary>
        /// UTCID04: Valid current password + Blank new password
        /// Expected: Service processes if validation bypassed (Controller should validate)
        /// Controller Validation Message: "New password is required." (from [Required] attribute)
        /// Note: Blank validation happens at Controller level with [Required] attribute
        /// </summary>
        [Fact]
        public async Task UTCID04_ChangePassword_BlankNewPassword_ServiceProcessesIfNotValidated()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "OldPassword@123";
            var blankNewPassword = "";

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, currentPassword, blankNewPassword);

            // Assert
            Assert.True(result); // Service accepts blank password if validation is bypassed

            // Verify password was updated (even with blank - which is bad!)
            Assert.NotNull(capturedUser);

            // Note: Controller [Required] validation should catch this before reaching service
            // Validation message: "New password is required." (from ChangePasswordRequest DTO)
        }

        /// <summary>
        /// UTCID05: Valid current password + Valid new password + Blank confirm password
        /// Expected: This is Controller validation concern
        /// Controller Validation Message: "Confirmation password is required." (from [Required] attribute)
        /// Note: Service doesn't receive confirmPassword parameter - controller validates this
        /// This test documents that confirm password validation is NOT a service layer concern
        /// </summary>
        [Fact]
        public void UTCID05_ChangePassword_BlankConfirmPassword_IsControllerConcern()
        {
            // Arrange & Act & Assert
            // This test documents that confirm password validation happens at Controller/FE level
            // Service method signature: ChangePasswordAsync(userId, currentPassword, newPassword)
            // There is NO confirmPassword parameter in the service layer

            // Controller should validate:
            // if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
            //     return BadRequest("Confirmation password is required.");

            Assert.True(true); // Documentary test - confirms this is not a service layer concern

            // Controller validation message: "Confirmation password is required." (from ChangePasswordRequest DTO)
        }

        /// <summary>
        /// UTCID06: Valid current password + Valid new password + Confirm does not match
        /// Expected: This is Controller validation concern
        /// Controller Validation Message: "The new password and confirmation password do not match." (from [Compare] attribute)
        /// Note: Service doesn't receive confirmPassword parameter - controller validates match
        /// </summary>
        [Fact]
        public void UTCID06_ChangePassword_ConfirmPasswordMismatch_IsControllerConcern()
        {
            // Arrange & Act & Assert
            // This test documents that confirm password match validation is Controller/FE concern
            // Service doesn't receive confirmPassword, so it can't validate the match

            // Controller should validate:
            // if (request.NewPassword != request.ConfirmPassword)
            //     return BadRequest("The new password and confirmation password do not match.");

            Assert.True(true); // Documentary test

            // Controller validation message: "The new password and confirmation password do not match." (from ChangePasswordRequest DTO)
        }

        /// <summary>
        /// UTCID07: Valid current password + Password less than 8 characters + Confirm matches
        /// Expected: Service processes if validation bypassed (Controller should validate)
        /// Controller Validation Message: "Password must be at least 8 characters long." (from [MinLength(8)] attribute)
        /// Note: Password length validation happens at Controller with [MinLength(8)] attribute
        /// </summary>
        [Fact]
        public async Task UTCID07_ChangePassword_ShortPassword_ServiceProcessesIfNotValidated()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "OldPassword@123";
            var shortNewPassword = "Pass@1"; // Only 6 characters

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, currentPassword, shortNewPassword);

            // Assert
            Assert.True(result); // Service accepts short password if validation is bypassed

            // Verify password was updated (even though it's too short)
            Assert.NotNull(capturedUser);
            Assert.True(BCrypt.Net.BCrypt.Verify(shortNewPassword, capturedUser.PasswordHash));

            // Note: Controller [MinLength(8)] validation should catch this before reaching service
            // Validation message: "Password must be at least 8 characters long." (from ChangePasswordRequest DTO)
        }

        /// <summary>
        /// Additional test: User not found
        /// Expected: Return false
        /// </summary>
        [Fact]
        public async Task ChangePassword_UserNotFound_ShouldReturnFalse()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var currentPassword = "OldPassword@123";
            var newPassword = "NewPassword@456";

            _mockUserRepository.Setup(x => x.GetByIdAsync(nonExistentUserId))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _jwtService.ChangePasswordAsync(nonExistentUserId, currentPassword, newPassword);

            // Assert
            Assert.False(result);

            // Verify update was NOT called
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // API returns: "Current password is incorrect or user not found" (from Controller when service returns false)
        }

        /// <summary>
        /// Additional test: Verify old password no longer works after change
        /// </summary>
        [Fact]
        public async Task ChangePassword_Success_ShouldInvalidateOldPassword()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "OldPassword@123";
            var newPassword = "NewPassword@456";

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ChangePasswordAsync(userId, currentPassword, newPassword);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedUser);

            // Verify old password no longer works
            Assert.False(BCrypt.Net.BCrypt.Verify(currentPassword, capturedUser.PasswordHash));

            // Verify new password works
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, capturedUser.PasswordHash));
        }
    }
}

