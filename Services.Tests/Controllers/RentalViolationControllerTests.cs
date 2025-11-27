using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.RentalViolationServices;
using ShareItAPI.Controllers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Services.Tests.Controllers
{
    public class RentalViolationControllerTests
    {
        private readonly Mock<IRentalViolationService> _serviceMock;
        private readonly RentalViolationController _controller;

        public RentalViolationControllerTests()
        {
            _serviceMock = new Mock<IRentalViolationService>();
            _controller = new RentalViolationController(_serviceMock.Object);
        }

        private void SetupProvider(Guid? providerId, string role = "provider")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, role)
            };

            if (providerId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, providerId.Value.ToString()));
            }

            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = context
            };
        }

        #region CreateMultipleViolations

        [Fact]
        public async Task UTCID01_CreateViolations_WithValidProvider_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = Guid.NewGuid(),
                Violations = new List<CreateRentalViolationDto>
                {
                    new CreateRentalViolationDto
                    {
                        OrderItemId = Guid.NewGuid(),
                        ViolationType = ViolationType.DAMAGED,
                        Description = new string('a', 20),
                        PenaltyPercentage = 10,
                        PenaltyAmount = 100,
                        EvidenceFiles = new List<IFormFile>()
                    }
                }
            };
            var ids = new List<Guid> { Guid.NewGuid() };
            _serviceMock.Setup(s => s.CreateMultipleViolationsAsync(dto, providerId))
                .ReturnsAsync(ids);

            var result = await _controller.CreateMultipleViolations(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<Guid>>>(ok.Value);
            Assert.Equal(ids, response.Data);
        }

        [Fact]
        public async Task UTCID02_CreateViolations_MissingProvider_ShouldReturnUnauthorized()
        {
            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = Guid.NewGuid(),
                Violations = new List<CreateRentalViolationDto>()
            };
            SetupProvider(null);

            var result = await _controller.CreateMultipleViolations(dto);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(unauthorized.Value);
            Assert.Equal("Unable to identify provider", response.Message);
        }

        [Fact]
        public async Task UTCID03_CreateViolations_ServiceUnauthorized_ShouldReturnForbid()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = Guid.NewGuid(),
                Violations = new List<CreateRentalViolationDto>()
            };
            _serviceMock.Setup(s => s.CreateMultipleViolationsAsync(dto, providerId))
                .ThrowsAsync(new UnauthorizedAccessException("nope"));

            var result = await _controller.CreateMultipleViolations(dto);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UTCID04_CreateViolations_ServiceThrows_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = Guid.NewGuid(),
                Violations = new List<CreateRentalViolationDto>()
            };
            _serviceMock.Setup(s => s.CreateMultipleViolationsAsync(dto, providerId))
                .ThrowsAsync(new Exception("Order not found"));

            var result = await _controller.CreateMultipleViolations(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.Equal("Order not found", response.Message);
        }

        #endregion

        #region GetProviderViolations

        [Fact]
        public async Task UTCID05_GetProviderViolations_ShouldReturnList()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var expected = new List<RentalViolationDto>
            {
                new RentalViolationDto { ViolationId = Guid.NewGuid(), Description = "Item broken" }
            };
            _serviceMock.Setup(s => s.GetProviderViolationsAsync(providerId))
                .ReturnsAsync(expected);

            var result = await _controller.GetProviderViolations();

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<IEnumerable<RentalViolationDto>>>(ok.Value);
            Assert.Single(response.Data);
            Assert.Equal(expected.First().ViolationId, response.Data.First().ViolationId);
        }

        [Fact]
        public async Task UTCID06_GetProviderViolations_MissingProvider_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.GetProviderViolations();

            var unauthorized = Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task UTCID07_GetProviderViolations_ServiceThrows_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.GetProviderViolationsAsync(providerId))
                .ThrowsAsync(new Exception("error"));

            var result = await _controller.GetProviderViolations();

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.Equal("error", response.Message);
        }

        #endregion

        #region UpdateViolation

        [Fact]
        public async Task UTCID08_UpdateViolation_Success_ShouldReturnOk()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var violationId = Guid.NewGuid();
            var dto = new UpdateViolationDto { Description = "updated" };
            _serviceMock.Setup(s => s.UpdateViolationByProviderAsync(violationId, dto, providerId))
                .ReturnsAsync(true);

            var result = await _controller.UpdateViolation(violationId, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(ok.Value);
            Assert.Equal("Violation updated successfully", response.Message);
        }

        [Fact]
        public async Task UTCID09_UpdateViolation_NotFound_ShouldReturn404()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var violationId = Guid.NewGuid();
            var dto = new UpdateViolationDto();
            _serviceMock.Setup(s => s.UpdateViolationByProviderAsync(violationId, dto, providerId))
                .ReturnsAsync(false);

            var result = await _controller.UpdateViolation(violationId, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(notFound.Value);
            Assert.Equal("Violation not found", response.Message);
        }

        [Fact]
        public async Task UTCID10_UpdateViolation_ProviderUnauthorized_ShouldReturnForbid()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var dto = new UpdateViolationDto();
            _serviceMock.Setup(s => s.UpdateViolationByProviderAsync(It.IsAny<Guid>(), dto, providerId))
                .ThrowsAsync(new UnauthorizedAccessException("no access"));

            var result = await _controller.UpdateViolation(Guid.NewGuid(), dto);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UTCID11_UpdateViolation_InvalidOperation_ShouldReturnBadRequest()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            var dto = new UpdateViolationDto();
            _serviceMock.Setup(s => s.UpdateViolationByProviderAsync(It.IsAny<Guid>(), dto, providerId))
                .ThrowsAsync(new InvalidOperationException("Wrong status"));

            var result = await _controller.UpdateViolation(Guid.NewGuid(), dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.Equal("Wrong status", response.Message);
        }

        [Fact]
        public async Task UTCID12_UpdateViolation_MissingProvider_ShouldReturnUnauthorized()
        {
            SetupProvider(null);

            var result = await _controller.UpdateViolation(Guid.NewGuid(), new UpdateViolationDto());

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task UTCID13_UpdateViolation_UnexpectedError_ShouldReturn500()
        {
            var providerId = Guid.NewGuid();
            SetupProvider(providerId);
            _serviceMock.Setup(s => s.UpdateViolationByProviderAsync(It.IsAny<Guid>(), It.IsAny<UpdateViolationDto>(), providerId))
                .ThrowsAsync(new Exception("boom"));

            var result = await _controller.UpdateViolation(Guid.NewGuid(), new UpdateViolationDto());

            var error = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, error.StatusCode);
            var response = Assert.IsType<ApiResponse<string>>(error.Value);
            Assert.StartsWith("Internal server error", response.Message);
        }

        #endregion
    }
}

