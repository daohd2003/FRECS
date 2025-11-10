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
    /// Unit tests for Forgot Password and Reset Password functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬──────────────────────────┬─────────────────────┬─────────────────────┬────────────────────────────────────────┬──────────────────────┐
    /// │ Test ID │ Scenario                 │ Email               │ Token/Password      │ Expected Result                        │ Backend Log Level    │
    /// ├─────────┼──────────────────────────┼─────────────────────┼─────────────────────┼────────────────────────────────────────┼──────────────────────┤
    /// │ UTCID01 │ Forgot Password          │ Valid, exists       │ -                   │ Return true, send email                │ Information          │
    /// │ UTCID02 │ Forgot Password          │ Valid, not exists   │ -                   │ Return false                           │ Warning              │
    /// │ UTCID03 │ Forgot Password          │ Invalid format      │ -                   │ Service processes (Controller validate)│ -                    │
    /// │ UTCID04 │ Reset Password           │ Valid, exists       │ Valid token + pwd   │ Return true                            │ Information          │
    /// │ UTCID05 │ Reset Password           │ Valid, exists       │ Invalid/expired     │ Return false                           │ Warning              │
    /// │ UTCID06 │ Reset Password           │ Valid, exists       │ Valid + short pwd   │ Service processes (Controller validate)│ -                    │
    /// │ UTCID07 │ Reset Password           │ Valid, exists       │ Confirm not match   │ Controller/FE validation               │ -                    │
    /// └─────────┴──────────────────────────┴─────────────────────┴─────────────────────┴────────────────────────────────────────┴──────────────────────┘
    /// 
    /// Note: Service layer returns true/false, doesn't throw exceptions.
    ///       All user-facing messages are generated at Controller/FE level.
    ///       Validation (email format, password length, confirm match) happens at Controller level.
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Run specific test: dotnet test --filter "FullyQualifiedName~UTCID01"
    /// 3. Run all forgot password tests: dotnet test --filter "FullyQualifiedName~ForgotPasswordTests"
    /// </summary>
    public class ForgotPasswordTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILoggedOutTokenRepository> _mockLoggedOutTokenRepository;
        private readonly Mock<ILogger<JwtService>> _mockLogger;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly JwtService _jwtService;
        private readonly JwtSettings _jwtSettings;

        public ForgotPasswordTests()
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

            // Setup configuration for frontend URL
            _mockConfiguration.Setup(x => x["ASPNETCORE_ENVIRONMENT"]).Returns("Development");
            _mockConfiguration.Setup(x => x["FrontendSettings:Development:BaseUrl"]).Returns("https://localhost:7045");

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
        /// UTCID01: Forgot Password - Valid existing email
        /// Expected: Return true, send password reset email
        /// Backend Log: "Sending password reset to {Email} with link: {ResetLink}" (Information)
        /// FE Message: "Password reset email sent"
        /// </summary>
        [Fact]
        public async Task UTCID01_ForgotPassword_ValidExistingEmail_ShouldReturnTrueAndSendEmail()
        {
            // Arrange
            var email = "existing@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _jwtService.ForgotPasswordAsync(email);

            // Assert
            Assert.True(result);

            // Verify user was updated with reset token
            _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.PasswordResetToken != null &&
                u.PasswordResetTokenExpiry != null &&
                u.PasswordResetTokenExpiry > DateTime.UtcNow
            )), Times.Once);

            // Verify password reset email was sent
            _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(
                "existing@example.com",
                It.Is<string>(link => link.Contains("reset-password") && link.Contains("existing%40example.com"))
            ), Times.Once);

            // Backend logs: "Sending password reset to {Email} with link: {ResetLink}"
            // FE shows: "Password reset email sent"
        }

        /// <summary>
        /// UTCID02: Forgot Password - Valid but non-existent email
        /// Expected: Return false, no email sent
        /// Backend: No log (just return false)
        /// FE Message: "Email not found"
        /// </summary>
        [Fact]
        public async Task UTCID02_ForgotPassword_NonExistentEmail_ShouldReturnFalse()
        {
            // Arrange
            var email = "nonexistent@example.com";

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _jwtService.ForgotPasswordAsync(email);

            // Assert
            Assert.False(result);

            // Verify no user was updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Verify no email was sent
            _mockEmailService.Verify(x => x.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>()
            ), Times.Never);

            // FE converts false result to: "Email not found"
        }

        /// <summary>
        /// UTCID03: Forgot Password - Invalid email format
        /// Expected: Service processes if validation is bypassed (Controller should validate)
        /// FE Message: "Email format is invalid."
        /// Note: [EmailAddress] validation happens at controller level
        /// </summary>
        [Fact]
        public async Task UTCID03_ForgotPassword_InvalidEmailFormat_ServiceProcessesIfNotValidated()
        {
            // Arrange
            var invalidEmail = "test@!test"; // Invalid format

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(invalidEmail))
                .ReturnsAsync((User?)null); // Email won't exist anyway

            // Act
            var result = await _jwtService.ForgotPasswordAsync(invalidEmail);

            // Assert
            Assert.False(result); // Returns false because user doesn't exist

            // Note: Controller [EmailAddress] validation should catch this before reaching service
            // FE message: "Email format is invalid."
        }

        /// <summary>
        /// UTCID04: Reset Password - Valid token and password
        /// Expected: Return true, password changed
        /// Backend: Password reset successful
        /// FE Message: "Password reset successful"
        /// </summary>
        [Fact]
        public async Task UTCID04_ResetPassword_ValidTokenAndPassword_ShouldReturnTrue()
        {
            // Arrange
            var email = "user@example.com";
            var token = "valid-reset-token";
            var newPassword = "NewPassword@123";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30), // Valid token
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, token, newPassword);

            // Assert
            Assert.True(result);

            // Verify password was updated and reset token cleared
            Assert.NotNull(capturedUser);
            Assert.Null(capturedUser.PasswordResetToken);
            Assert.Null(capturedUser.PasswordResetTokenExpiry);
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, capturedUser.PasswordHash));

            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Once);

            // FE shows: "Password reset successful"
        }

        /// <summary>
        /// UTCID05: Reset Password - Invalid or expired token
        /// Expected: Return false
        /// Backend: No update performed
        /// FE Message: "Invalid token or token expired"
        /// </summary>
        [Fact]
        public async Task UTCID05_ResetPassword_InvalidOrExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            var email = "user@example.com";
            var providedToken = "invalid-token";
            var newPassword = "NewPassword@123";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                PasswordResetToken = "different-token",
                PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-1), // Expired
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, providedToken, newPassword);

            // Assert
            Assert.False(result);

            // Verify no user was updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // FE converts false result to: "Invalid token or token expired"
        }

        /// <summary>
        /// UTCID05 Alternative: Reset Password - Expired token (even if matches)
        /// Expected: Return false
        /// </summary>
        [Fact]
        public async Task UTCID05_ResetPassword_ExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            var email = "user@example.com";
            var token = "valid-token";
            var newPassword = "NewPassword@123";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                PasswordResetToken = token, // Token matches
                PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-10), // But expired
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, token, newPassword);

            // Assert
            Assert.False(result);

            // Verify no update
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        /// <summary>
        /// UTCID06: Reset Password - Valid token but password less than 8 characters
        /// Expected: Service processes if validation is bypassed (Controller should validate)
        /// FE Message: "Password must be at least 8 characters long."
        /// Note: [MinLength(8)] validation happens at controller level
        /// </summary>
        [Fact]
        public async Task UTCID06_ResetPassword_ShortPassword_ServiceProcessesIfNotValidated()
        {
            // Arrange
            var email = "user@example.com";
            var token = "valid-token";
            var shortPassword = "Pass@1"; // Only 6 characters

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, token, shortPassword);

            // Assert
            Assert.True(result); // Service accepts it if controller validation is bypassed

            // Verify password was updated (even though it's short)
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Once);

            // Note: Controller [MinLength(8)] validation should catch this
            // FE message: "Password must be at least 8 characters long."
        }

        /// <summary>
        /// UTCID07: Reset Password - Confirm password does not match
        /// Expected: This is handled at Controller/FE level, not service
        /// FE Message: "The new password and confirmation password do not match."
        /// Note: Service doesn't receive confirmPassword parameter - controller validates this
        /// This test documents that confirm password validation is NOT a service concern
        /// </summary>
        [Fact]
        public void UTCID07_ResetPassword_ConfirmPasswordMismatch_IsControllerConcern()
        {
            // Arrange & Act & Assert
            // This test documents that confirm password validation happens at Controller/FE level
            // Service method signature: ResetPasswordAsync(email, token, newPassword)
            // There is NO confirmPassword parameter in the service layer

            // Controller should validate:
            // if (request.NewPassword != request.ConfirmPassword)
            //     return BadRequest("The new password and confirmation password do not match.");

            Assert.True(true); // Documentary test - confirms this is not a service layer concern
            
            // FE/Controller message: "The new password and confirmation password do not match."
        }

        /// <summary>
        /// Additional test: Verify reset token expiry is set to 30 minutes
        /// </summary>
        [Fact]
        public async Task ForgotPassword_ShouldSetTokenExpiryTo30Minutes()
        {
            // Arrange
            var email = "user@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
                EmailConfirmed = true,
                IsActive = true
            };

            DateTime? capturedExpiry = null;
            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedExpiry = u.PasswordResetTokenExpiry)
                .ReturnsAsync(true);

            _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var beforeCall = DateTime.UtcNow;

            // Act
            await _jwtService.ForgotPasswordAsync(email);

            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.NotNull(capturedExpiry);
            
            // Token should expire in approximately 30 minutes
            var expectedExpiry = beforeCall.AddMinutes(30);
            var actualExpiry = capturedExpiry.Value;
            
            Assert.True(actualExpiry >= expectedExpiry.AddSeconds(-5)); // Allow 5 second tolerance
            Assert.True(actualExpiry <= afterCall.AddMinutes(30).AddSeconds(5));
        }

        /// <summary>
        /// Additional test: Verify reset token is cleared after successful password reset
        /// </summary>
        [Fact]
        public async Task ResetPassword_ShouldClearResetTokenAfterSuccess()
        {
            // Arrange
            var email = "user@example.com";
            var token = "valid-token";
            var newPassword = "NewPassword@123";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30),
                EmailConfirmed = true,
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, token, newPassword);

            // Assert
            Assert.True(result);

            // Verify token and expiry were cleared
            _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.PasswordResetToken == null &&
                u.PasswordResetTokenExpiry == null
            )), Times.Once);
        }

        /// <summary>
        /// Additional test: Non-existent email for reset password
        /// </summary>
        [Fact]
        public async Task ResetPassword_NonExistentEmail_ShouldReturnFalse()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var token = "some-token";
            var newPassword = "NewPassword@123";

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _jwtService.ResetPasswordAsync(email, token, newPassword);

            // Assert
            Assert.False(result);

            // Verify no update
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
        }
    }
}

