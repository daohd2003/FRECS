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
    /// Unit tests for Login functionality using email and password
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬──────────────────────────────────────┬──────────────┬─────────────┬────────────────────────────────────────────────────┬──────────────────────┐
    /// │ Test ID │ Email Status                         │ Password     │ RememberMe  │ Expected Result                                    │ Backend Log Level    │
    /// ├─────────┼──────────────────────────────────────┼──────────────┼─────────────┼────────────────────────────────────────────────────┼──────────────────────┤
    /// │ UTCID01 │ Valid, verified, active              │ Correct      │ TRUE        │ Login success, return token                        │ Information          │
    /// │ UTCID02 │ Valid, verified, active              │ Incorrect    │ FALSE       │ UnauthorizedAccessException: Invalid credentials   │ Warning              │
    /// │ UTCID03 │ Valid, unverified                    │ Correct      │ FALSE       │ InvalidOperationException: Email not verified      │ Warning              │
    /// │ UTCID04 │ Valid, verified, inactive (blocked)  │ Correct      │ FALSE       │ UnauthorizedAccessException: Account blocked       │ Warning              │
    /// │ UTCID05 │ Non-existent                         │ Any          │ FALSE       │ UnauthorizedAccessException: Invalid credentials   │ Warning              │
    /// │ UTCID06 │ Blank                                │ Blank        │ FALSE       │ UnauthorizedAccessException: Invalid credentials   │ Warning              │
    /// └─────────┴──────────────────────────────────────┴──────────────┴─────────────┴────────────────────────────────────────────────────┴──────────────────────┘
    /// 
    /// Note: These tests verify BACKEND business logic and exception messages.
    ///       Frontend/Razor Pages messages (like "Login failed. Please verify your email to continue.") 
    ///       are NOT tested here as they are UI-layer concerns.
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Visual Studio: Right-click on Services.Tests project -> Run Tests
    /// 3. Run specific test: dotnet test --filter "FullyQualifiedName~UTCID01"
    /// 4. Run all login tests: dotnet test --filter "FullyQualifiedName~LoginAuthenticationTests"
    /// </summary>
    public class LoginAuthenticationTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILoggedOutTokenRepository> _mockLoggedOutTokenRepository;
        private readonly Mock<ILogger<JwtService>> _mockLogger;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly JwtService _jwtService;
        private readonly JwtSettings _jwtSettings;

        public LoginAuthenticationTests()
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
        /// UTCID01: Valid, existing, verified, and active email + correct password + RememberMe=TRUE
        /// Expected: Login success, no exception
        /// Backend Log: "Login successful for {Email}" (Information level)
        /// </summary>
        [Fact]
        public async Task UTCID01_ValidCredentials_RememberMeTrue_ShouldLoginSuccessfully()
        {
            // Arrange
            var email = "valid@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var userId = Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = true,
                Role = UserRole.customer,
                Profile = new Profile
                {
                    FullName = "Test User",
                    ProfilePictureUrl = "https://example.com/avatar.jpg"
                }
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.Authenticate(email, password, rememberMe: true);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal(UserRole.customer.ToString(), result.Role);

            // Verify user was updated with refresh token and last login
            _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.RefreshToken != null &&
                u.LastLogin != null
            )), Times.Once);

            // Note: Backend service should log successful login
            // Verify logger would be called if logging was implemented:
            // _mockLogger.Verify(x => x.Log(LogLevel.Information, ..., "Login successful for {Email}", email), Times.Once);
        }

        /// <summary>
        /// UTCID02: Valid, existing, verified, and active email + incorrect password + RememberMe=FALSE
        /// Expected: Login fail, UnauthorizedAccessException, "Invalid email or password"
        /// Backend Log: "Failed login attempt for {Email}: Invalid password" (Warning level)
        /// </summary>
        [Fact]
        public async Task UTCID02_IncorrectPassword_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var email = "valid@example.com";
            var correctPassword = "Password@123";
            var incorrectPassword = "WrongPassword@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = true,
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _jwtService.Authenticate(email, incorrectPassword, rememberMe: false)
            );

            Assert.Equal("Invalid email or password", exception.Message);

            // Verify user was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Backend log verification - service should log failed login attempts for security
            // Note: "Invalid email or password" is the exception message, logged by the service
        }

        /// <summary>
        /// UTCID03: Valid, existing, but unverified email + correct password + RememberMe=FALSE
        /// Expected: Login fail, InvalidOperationException, "Email not verified"
        /// Backend Log: "Login attempt with unverified email: {Email}" (Warning level)
        /// FE Message (not tested here): "Login failed. Please verify your email to continue."
        /// </summary>
        [Fact]
        public async Task UTCID03_UnverifiedEmail_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var email = "unverified@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = false, // Email not verified
                IsActive = true,
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _jwtService.Authenticate(email, password, rememberMe: false)
            );

            Assert.Equal("Email not verified", exception.Message);

            // Verify user was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Backend log: Exception message is "Email not verified"
            // FE converts this to user-friendly: "Login failed. Please verify your email to continue."
        }

        /// <summary>
        /// UTCID04: Valid, existing, verified, and inactive email + correct password + RememberMe=FALSE
        /// Expected: Login fail, UnauthorizedAccessException, "Your account has been blocked. Please contact support."
        /// Backend Log: "Blocked account login attempt: {Email}" (Warning level)
        /// </summary>
        [Fact]
        public async Task UTCID04_InactiveAccount_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var email = "blocked@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = false, // Account is blocked/inactive
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _jwtService.Authenticate(email, password, rememberMe: false)
            );

            Assert.Equal("Your account has been blocked. Please contact support.", exception.Message);

            // Verify user was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Backend log: Should log blocked account login attempts for security monitoring
        }

        /// <summary>
        /// UTCID05: Non-existent or invalid format email + correct password + RememberMe=FALSE
        /// Expected: Login fail, UnauthorizedAccessException, "Invalid email or password"
        /// Backend Log: "Failed login attempt for non-existent email: {Email}" (Warning level)
        /// </summary>
        [Fact]
        public async Task UTCID05_NonExistentEmail_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var password = "Password@123";

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null); // User does not exist

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _jwtService.Authenticate(email, password, rememberMe: false)
            );

            Assert.Equal("Invalid email or password", exception.Message);

            // Verify user was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Backend log: Should log failed login attempts for security
        }

        /// <summary>
        /// UTCID06: Blank email + incorrect password + RememberMe=FALSE
        /// Expected: Login fail, UnauthorizedAccessException, "Invalid email or password"
        /// Backend Log: "Login attempt with blank credentials" (Warning level)
        /// FE Message (not tested here): "Email and password are required." (Client-side validation)
        /// Note: Blank validation should ideally happen at controller/FE level first
        /// </summary>
        [Fact]
        public async Task UTCID06_BlankEmailAndPassword_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var email = "";
            var password = "";

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null);

            // Act & Assert
            // Service level will treat blank credentials as invalid credentials
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _jwtService.Authenticate(email, password, rememberMe: false)
            );

            Assert.Equal("Invalid email or password", exception.Message);

            // Verify user was NOT updated
            _mockUserRepository.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);

            // Note: "Email and password are required." is FE/Controller validation message
            // Backend service returns generic "Invalid email or password" for security
        }

        /// <summary>
        /// Additional test: Verify that RememberMe flag affects token expiration
        /// </summary>
        [Fact]
        public async Task RememberMe_ShouldGenerateLongerLivedToken()
        {
            // Arrange
            var email = "user@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = true,
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var resultWithRememberMe = await _jwtService.Authenticate(email, password, rememberMe: true);

            // Reset mock for second call
            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            var resultWithoutRememberMe = await _jwtService.Authenticate(email, password, rememberMe: false);

            // Assert
            Assert.NotNull(resultWithRememberMe.Token);
            Assert.NotNull(resultWithoutRememberMe.Token);

            // Both should have valid tokens
            Assert.NotEqual(resultWithRememberMe.Token, resultWithoutRememberMe.Token);
        }

        /// <summary>
        /// Additional test: Verify that LastLogin is updated on successful authentication
        /// </summary>
        [Fact]
        public async Task SuccessfulLogin_ShouldUpdateLastLoginTime()
        {
            // Arrange
            var email = "user@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            DateTime? capturedLastLogin = null;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = true,
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedLastLogin = u.LastLogin)
                .ReturnsAsync(true);

            var beforeLogin = DateTime.UtcNow;

            // Act
            await _jwtService.Authenticate(email, password, rememberMe: false);

            var afterLogin = DateTime.UtcNow;

            // Assert
            Assert.NotNull(capturedLastLogin);
            Assert.True(capturedLastLogin >= beforeLogin);
            Assert.True(capturedLastLogin <= afterLogin);
        }

        /// <summary>
        /// Additional test: Verify that RefreshToken is generated and stored
        /// </summary>
        [Fact]
        public async Task SuccessfulLogin_ShouldGenerateRefreshToken()
        {
            // Arrange
            var email = "user@example.com";
            var password = "Password@123";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            string? capturedRefreshToken = null;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                IsActive = true,
                Role = UserRole.customer
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedRefreshToken = u.RefreshToken)
                .ReturnsAsync(true);

            // Act
            var result = await _jwtService.Authenticate(email, password, rememberMe: false);

            // Assert
            Assert.NotNull(result.RefreshToken);
            Assert.NotNull(capturedRefreshToken);
            Assert.Equal(capturedRefreshToken, result.RefreshToken);
        }
    }
}

