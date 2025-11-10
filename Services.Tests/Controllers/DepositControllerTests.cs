using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.DepositDto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DepositServices;
using ShareItAPI.Controllers;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for DepositController (API Layer)
    /// Tests view deposit history functionality at controller level
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~DepositControllerTests"
    /// </summary>
    public class DepositControllerTests
    {
        private readonly Mock<IDepositService> _mockService;
        private readonly DepositController _controller;
        private readonly Guid _testCustomerId;

        public DepositControllerTests()
        {
            _mockService = new Mock<IDepositService>();
            _controller = new DepositController(_mockService.Object);
            _testCustomerId = Guid.NewGuid();

            // Setup user claims for authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _testCustomerId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        /// <summary>
        /// Get deposit stats - Valid request
        /// Expected: 200 OK with deposit statistics
        /// </summary>
        [Fact]
        public async Task GetDepositStats_ValidRequest_ShouldReturn200WithStats()
        {
            // Arrange
            var stats = new DepositStatsDto
            {
                DepositsRefunded = 100,
                PendingRefunds = 50,
                RefundIssues = 2
            };

            _mockService.Setup(x => x.GetDepositStatsAsync(_testCustomerId))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetDepositStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<DepositStatsDto>>(okResult.Value);
            Assert.Equal("Deposit statistics retrieved", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(100, apiResponse.Data.DepositsRefunded);

            _mockService.Verify(x => x.GetDepositStatsAsync(_testCustomerId), Times.Once);
        }

        /// <summary>
        /// Get deposit history - Valid request
        /// Expected: 200 OK with deposit history list
        /// </summary>
        [Fact]
        public async Task GetDepositHistory_ValidRequest_ShouldReturn200WithHistory()
        {
            // Arrange
            var history = new List<DepositHistoryDto>
            {
                new DepositHistoryDto
                {
                    OrderId = Guid.NewGuid(),
                    DepositAmount = 50,
                    Status = BusinessObject.Enums.TransactionStatus.completed
                }
            };

            _mockService.Setup(x => x.GetDepositHistoryAsync(_testCustomerId))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetDepositHistory();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<List<DepositHistoryDto>>>(okResult.Value);
            Assert.Equal("Deposit history retrieved", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            _mockService.Verify(x => x.GetDepositHistoryAsync(_testCustomerId), Times.Once);
        }

        /// <summary>
        /// Get deposit history - Empty result
        /// Expected: 200 OK with empty list
        /// </summary>
        [Fact]
        public async Task GetDepositHistory_EmptyResult_ShouldReturn200WithEmptyList()
        {
            // Arrange
            _mockService.Setup(x => x.GetDepositHistoryAsync(_testCustomerId))
                .ReturnsAsync(new List<DepositHistoryDto>());

            // Act
            var result = await _controller.GetDepositHistory();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<DepositHistoryDto>>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
            Assert.Empty(apiResponse.Data);
        }

        /// <summary>
        /// Unauthorized access - No user claims
        /// Expected: 401 Unauthorized
        /// </summary>
        [Fact]
        public async Task GetDepositStats_NoUserClaims_ShouldReturn401()
        {
            // Arrange
            var controller = new DepositController(_mockService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await controller.GetDepositStats();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
        }
    }
}

