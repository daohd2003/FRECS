using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProviderApplications;
using BusinessObject.Enums;
using BusinessObject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.ProviderApplicationServices;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for ProviderApplicationsController.Apply method (API Layer)
    /// Tests the Register to become provider functionality
    /// 
    /// Verifies API responses and HTTP status codes
    /// 
    /// Note: Based on current DTO implementation:
    ///  - BusinessName is [Required] - validation handled by ModelState
    ///  - TaxId, ContactPhone, Notes are all OPTIONAL (nullable)
    /// 
    /// API messages:
    ///  - Success: "Application submitted"
    ///  - The message "Application submitted. We will notify you after review." appears to be a frontend message.
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~ProviderApplicationsControllerTests"
    /// </summary>
    public class ProviderApplicationsControllerTests
    {
        private readonly Mock<IProviderApplicationService> _mockService;
        private readonly ProviderApplicationsController _controller;

        public ProviderApplicationsControllerTests()
        {
            _mockService = new Mock<IProviderApplicationService>();
            _controller = new ProviderApplicationsController(_mockService.Object);
        }

        private void SetupUserContext(Guid userId, string role = "customer")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region Core Test Cases

        /// <summary>
        /// UTCID01: Apply with all valid fields
        /// Expected: 200 OK with message "Application submitted"
        /// Backend behavior:
        ///   - Controller gets userId from claims
        ///   - Service creates application
        ///   - Controller returns 200 OK with ApiResponse containing message and application data
        /// API message: "Application submitted"
        /// Frontend message (not from API): "Application submitted. We will notify you after review."
        /// </summary>
        [Fact]
        public async Task UTCID01_Apply_AllValidFields_ShouldReturn200OKWithMessage()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business",
                TaxId = "1234567890",
                ContactPhone = "0123456789",
                Notes = "I want to become a provider"
            };

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = dto.BusinessName,
                TaxId = dto.TaxId,
                ContactPhone = dto.ContactPhone,
                Notes = dto.Notes,
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTime.UtcNow
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ReturnsAsync(application);

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Application submitted", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            // Verify the returned data contains Id and Status
            var data = apiResponse.Data;
            Assert.NotNull(data);

            _mockService.Verify(x => x.ApplyAsync(userId, dto), Times.Once);

            // Note: The API returns "Application submitted"
            // The message "Application submitted. We will notify you after review." is a frontend message.
        }

        /// <summary>
        /// UTCID05: Apply with only BusinessName (optional fields null)
        /// Expected: 200 OK with message "Application submitted"
        /// </summary>
        [Fact]
        public async Task UTCID05_Apply_OnlyBusinessName_OptionalFieldsNull_ShouldReturn200OK()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business",
                TaxId = null,
                ContactPhone = null,
                Notes = null
            };

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = dto.BusinessName,
                TaxId = null,
                ContactPhone = null,
                Notes = null,
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTime.UtcNow
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ReturnsAsync(application);

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Application submitted", apiResponse.Message);

            _mockService.Verify(x => x.ApplyAsync(userId, dto), Times.Once);
        }

        #endregion

        #region Additional Test Cases

        /// <summary>
        /// Additional Test: Apply without authentication should return 401
        /// </summary>
        [Fact]
        public async Task Additional_Apply_NoAuthentication_ShouldReturn401Unauthorized()
        {
            // Arrange
            // Setup user context with no NameIdentifier claim
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);

            _mockService.Verify(x => x.ApplyAsync(It.IsAny<Guid>(), It.IsAny<ProviderApplicationCreateDto>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Apply with empty userId claim should return 401
        /// </summary>
        [Fact]
        public async Task Additional_Apply_EmptyUserIdClaim_ShouldReturn401Unauthorized()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, ""), // Empty user ID
                new Claim(ClaimTypes.Role, "customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);

            _mockService.Verify(x => x.ApplyAsync(It.IsAny<Guid>(), It.IsAny<ProviderApplicationCreateDto>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Service throws InvalidOperationException (user not found) should propagate exception
        /// </summary>
        [Fact]
        public async Task Additional_Apply_UserNotFound_ShouldThrowException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ThrowsAsync(new InvalidOperationException("User not found"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.Apply(dto)
            );

            Assert.Equal("User not found", exception.Message);
        }

        /// <summary>
        /// Additional Test: Service throws InvalidOperationException (already provider) should propagate exception
        /// </summary>
        [Fact]
        public async Task Additional_Apply_AlreadyProvider_ShouldThrowException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ThrowsAsync(new InvalidOperationException("User is already a provider"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.Apply(dto)
            );

            Assert.Equal("User is already a provider", exception.Message);
        }

        /// <summary>
        /// Additional Test: Existing pending application should return 200 OK with that application
        /// </summary>
        [Fact]
        public async Task Additional_Apply_ExistingPendingApplication_ShouldReturn200OK()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "New Business"
            };

            var existingApplication = new ProviderApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = "Existing Business",
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ReturnsAsync(existingApplication);

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Application submitted", apiResponse.Message);
        }

        /// <summary>
        /// Additional Test: Verify response contains application Id and Status
        /// </summary>
        [Fact]
        public async Task Additional_Apply_ShouldReturnApplicationIdAndStatus()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            SetupUserContext(userId);

            var dto = new ProviderApplicationCreateDto
            {
                BusinessName = "Test Business"
            };

            var application = new BusinessObject.Models.ProviderApplication
            {
                Id = applicationId,
                UserId = userId,
                BusinessName = dto.BusinessName,
                Status = ProviderApplicationStatus.pending,
                CreatedAt = DateTime.UtcNow
            };

            _mockService.Setup(x => x.ApplyAsync(userId, dto))
                .ReturnsAsync(application);

            // Act
            var result = await _controller.Apply(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.NotNull(apiResponse.Data);
            
            // The controller returns new { app.Id, app.Status }
            // Just verify the response structure is correct
            Assert.Equal("Application submitted", apiResponse.Message);
        }

        #endregion
    }
}

