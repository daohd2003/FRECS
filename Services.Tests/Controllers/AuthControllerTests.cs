using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Login;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.Authentication;
using Services.UserServices;
using ShareItAPI.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for Authentication Controller (API Layer)
    /// Verifies API messages and HTTP status codes
    /// 
    /// Test Coverage:
    /// - Login (POST /api/auth/login)
    /// - Register (POST /api/auth/register)
    /// - Forgot Password (POST /api/auth/forgot-password)
    /// - Reset Password (POST /api/auth/reset-password)
    /// - Change Password (POST /api/auth/change-password)
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~AuthControllerTests"
    /// </summary>
    public class AuthControllerTests
    {
        private readonly Mock<IJwtService> _mockJwtService;
        private readonly Mock<IUserService> _mockUserService;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockJwtService = new Mock<IJwtService>();
            _mockUserService = new Mock<IUserService>();
            
            // Pass null for GoogleAuthService and FacebookAuthService as they're not used in these tests
            _controller = new AuthController(
                _mockJwtService.Object,
                _mockUserService.Object,
                null!, // GoogleAuthService not needed for these tests
                null!  // FacebookAuthService not needed for these tests
            );
        }

        #region Login Tests

        /// <summary>
        /// Login - Success case
        /// API Message: "Login successful"
        /// </summary>
        [Fact]
        public async Task Login_ValidCredentials_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "Password@123",
                RememberMe = true
            };

            var tokenResponse = new TokenResponseDto
            {
                Token = "access_token",
                RefreshToken = "refresh_token",
                Role = UserRole.customer.ToString()
            };

            _mockJwtService.Setup(x => x.Authenticate(request.Email, request.Password, request.RememberMe))
                .ReturnsAsync(tokenResponse);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<TokenResponseDto>>(okResult.Value);
            Assert.Equal("Login successful", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("access_token", apiResponse.Data.Token);
        }

        /// <summary>
        /// Login - Invalid credentials
        /// API Message: "Invalid email or password"
        /// </summary>
        [Fact]
        public async Task Login_InvalidCredentials_ShouldReturn401WithErrorMessage()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "WrongPassword",
                RememberMe = false
            };

            _mockJwtService.Setup(x => x.Authenticate(request.Email, request.Password, request.RememberMe))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(unauthorizedResult.Value);
            Assert.Equal("Invalid email or password", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// Login - Blocked account
        /// API Message: Exception message containing "blocked"
        /// </summary>
        [Fact]
        public async Task Login_BlockedAccount_ShouldReturn401WithBlockedMessage()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "blocked@example.com",
                Password = "Password@123",
                RememberMe = false
            };

            _mockJwtService.Setup(x => x.Authenticate(request.Email, request.Password, request.RememberMe))
                .ThrowsAsync(new UnauthorizedAccessException("Your account has been blocked. Please contact support."));

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(unauthorizedResult.Value);
            Assert.Contains("blocked", apiResponse.Message.ToLower()); // Verify blocked message
        }

        /// <summary>
        /// Login - Unverified email
        /// API Message: "Login failed. Please verify your email to continue."
        /// </summary>
        [Fact]
        public async Task Login_UnverifiedEmail_ShouldReturn400WithVerificationMessage()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "unverified@example.com",
                Password = "Password@123",
                RememberMe = false
            };

            _mockJwtService.Setup(x => x.Authenticate(request.Email, request.Password, request.RememberMe))
                .ThrowsAsync(new InvalidOperationException("Email not verified"));

            // Act
            var result = await _controller.Login(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Login failed. Please verify your email to continue.", apiResponse.Message); // Verify API message
        }

        #endregion

        #region Register Tests

        /// <summary>
        /// Register - Success case
        /// API Message: "Registration successful! Please check your email to verify your account."
        /// </summary>
        [Fact]
        public async Task Register_ValidRequest_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var request = new RegisterRequest
            {
                FullName = "New User",
                Email = "newuser@example.com",
                Password = "Password@123"
            };

            var tokenResponse = new TokenResponseDto
            {
                Token = "access_token",
                RefreshToken = "refresh_token",
                Role = UserRole.customer.ToString()
            };

            _mockJwtService.Setup(x => x.RegisterAsync(request))
                .ReturnsAsync(tokenResponse);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<TokenResponseDto>>(okResult.Value);
            Assert.Equal("Registration successful! Please check your email to verify your account.", apiResponse.Message); // Verify API message
            Assert.NotNull(apiResponse.Data);
        }

        /// <summary>
        /// Register - Email already registered
        /// API Message: "Email is already registered"
        /// </summary>
        [Fact]
        public async Task Register_EmailAlreadyExists_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var request = new RegisterRequest
            {
                FullName = "Existing User",
                Email = "existing@example.com",
                Password = "Password@123"
            };

            _mockJwtService.Setup(x => x.RegisterAsync(request))
                .ReturnsAsync((TokenResponseDto?)null); // Returns null when email already exists

            // Act
            var result = await _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Email is already registered", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// Register - Email already registered but not verified
        /// API Message: Exception message containing "already registered but not verified"
        /// </summary>
        [Fact]
        public async Task Register_EmailExistsButUnverified_ShouldReturn400WithUnverifiedMessage()
        {
            // Arrange
            var request = new RegisterRequest
            {
                FullName = "Unverified User",
                Email = "unverified@example.com",
                Password = "Password@123"
            };

            _mockJwtService.Setup(x => x.RegisterAsync(request))
                .ThrowsAsync(new InvalidOperationException("Email is already registered but not verified. Please check your email for the verification link."));

            // Act
            var result = await _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("already registered but not verified", apiResponse.Message); // Verify API message
        }

        #endregion

        #region Forgot Password Tests

        /// <summary>
        /// Forgot Password - Success case
        /// API Message: "Password reset email sent"
        /// </summary>
        [Fact]
        public async Task ForgotPassword_ValidEmail_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var request = new ForgotPasswordRequest
            {
                Email = "existing@example.com"
            };

            _mockJwtService.Setup(x => x.ForgotPasswordAsync(request.Email))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ForgotPassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Password reset email sent", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// Forgot Password - Email not found
        /// API Message: "Email not found"
        /// </summary>
        [Fact]
        public async Task ForgotPassword_EmailNotFound_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var request = new ForgotPasswordRequest
            {
                Email = "nonexistent@example.com"
            };

            _mockJwtService.Setup(x => x.ForgotPasswordAsync(request.Email))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ForgotPassword(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Email not found", apiResponse.Message); // Verify API message
        }

        #endregion

        #region Reset Password Tests

        /// <summary>
        /// Reset Password - Success case
        /// API Message: "Password reset successful"
        /// </summary>
        [Fact]
        public async Task ResetPassword_ValidTokenAndPassword_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var request = new ResetPasswordRequest
            {
                Email = "user@example.com",
                Token = "valid_reset_token",
                NewPassword = "NewPassword@123"
            };

            _mockJwtService.Setup(x => x.ResetPasswordAsync(request.Email, request.Token, request.NewPassword))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ResetPassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Password reset successful", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// Reset Password - Invalid or expired token
        /// API Message: "Invalid token or token expired"
        /// </summary>
        [Fact]
        public async Task ResetPassword_InvalidToken_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var request = new ResetPasswordRequest
            {
                Email = "user@example.com",
                Token = "invalid_token",
                NewPassword = "NewPassword@123"
            };

            _mockJwtService.Setup(x => x.ResetPasswordAsync(request.Email, request.Token, request.NewPassword))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ResetPassword(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Invalid token or token expired", apiResponse.Message); // Verify API message
        }

        #endregion

        #region Change Password Tests

        /// <summary>
        /// Change Password - Success case
        /// API Message: "Password changed successfully"
        /// </summary>
        [Fact]
        public async Task ChangePassword_ValidRequest_ShouldReturn200WithSuccessMessage()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPassword@123",
                NewPassword = "NewPassword@123",
                ConfirmPassword = "NewPassword@123"
            };

            // Setup controller context with user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            _mockJwtService.Setup(x => x.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ChangePassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Password changed successfully", apiResponse.Message); // Verify API message
        }

        /// <summary>
        /// Change Password - Incorrect current password
        /// API Message: "Current password is incorrect or user not found"
        /// </summary>
        [Fact]
        public async Task ChangePassword_IncorrectCurrentPassword_ShouldReturn400WithErrorMessage()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new ChangePasswordRequest
            {
                CurrentPassword = "WrongPassword@123",
                NewPassword = "NewPassword@123",
                ConfirmPassword = "NewPassword@123"
            };

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            _mockJwtService.Setup(x => x.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ChangePassword(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Equal("Current password is incorrect or user not found", apiResponse.Message); // Verify API message
        }

        #endregion
    }
}

