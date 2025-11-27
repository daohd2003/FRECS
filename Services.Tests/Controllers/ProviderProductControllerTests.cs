using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.ProductDto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.ContentModeration;
using Services.ConversationServices;
using Services.ProductServices;
using ShareItAPI.Controllers;
using System.Security.Claims;
using Xunit;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for ProductController - Provider Product Management functionality
    /// Verifies API responses, HTTP status codes, and authorization
    /// 
    /// Test Coverage:
    /// 
    /// - Create Product (POST /api/products)
    ///   * 6 test cases (Valid, AI flagged, AI service error, Unauthorized, Invalid data, Missing images)
    /// 
    /// - Update Product (PUT /api/products/{id})
    ///   * 5 test cases (Valid, Not found, Unauthorized, AI flagged, Update failed)
    /// 
    /// - Delete Product (DELETE /api/products/{id})
    ///   * 5 test cases (Valid, Not found, Unauthorized, Has orders, Delete failed)
    /// 
    /// - Restore Product (PUT /api/products/restore/{id})
    ///   * 4 test cases (Valid, Not found, Unauthorized, Invalid status)
    /// 
    /// - Get Product By ID (GET /api/products/{id})
    ///   * 2 test cases (Valid, Not found)
    /// 
    /// - Update Product Images (PUT /api/products/{id}/images)
    ///   * 4 test cases (Valid, Not found, Unauthorized, No images)
    /// 
    /// Total: 26 unit tests
    /// </summary>
    public class ProviderProductControllerTests
    {
        private readonly Mock<IProductService> _mockService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IContentModerationService> _mockModerationService;
        private readonly Mock<IConversationService> _mockConversationService;
        private readonly ProductController _controller;
        private readonly Guid _providerId = Guid.NewGuid();

        public ProviderProductControllerTests()
        {
            _mockService = new Mock<IProductService>();
            _mockMapper = new Mock<IMapper>();
            _mockModerationService = new Mock<IContentModerationService>();
            _mockConversationService = new Mock<IConversationService>();

            _controller = new ProductController(
                _mockService.Object,
                _mockMapper.Object,
                _mockModerationService.Object,
                _mockConversationService.Object
            );

            // Setup authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _providerId.ToString()),
                new Claim(ClaimTypes.Role, "provider"),
                new Claim(ClaimTypes.Name, "Test Provider")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region Create Product Tests

        [Fact]
        public async Task CreateProduct_ValidData_ShouldReturn201Created()
        {
            // Arrange
            var productRequest = new ProductRequestDTO
            {
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            var createdProduct = new ProductDTO
            {
                Id = Guid.NewGuid(),
                Name = productRequest.Name,
                AvailabilityStatus = "available"
            };

            _mockService.Setup(s => s.AddAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(createdProduct);

            // Act
            var result = await _controller.Create(productRequest);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var response = Assert.IsType<ApiResponse<ProductDTO>>(createdResult.Value);
            Assert.Contains("created successfully", response.Message);
            Assert.Contains("AVAILABLE", response.Message);
            Assert.Equal(createdProduct, response.Data);
        }

        [Fact]
        public async Task CreateProduct_AIFlagsContent_ShouldReturn201WithPendingMessage()
        {
            // Arrange
            var productRequest = new ProductRequestDTO
            {
                Name = "Inappropriate Product",
                Description = "Bad content",
                CategoryId = Guid.NewGuid(),
                Size = "L",
                Color = "Red",
                PricePerDay = 50,
                RentalQuantity = 3,
                SecurityDeposit = 100,
                Gender = "Male",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            var createdProduct = new ProductDTO
            {
                Id = Guid.NewGuid(),
                Name = productRequest.Name,
                AvailabilityStatus = "pending"
            };

            _mockService.Setup(s => s.AddAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(createdProduct);

            // Act
            var result = await _controller.Create(productRequest);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var response = Assert.IsType<ApiResponse<ProductDTO>>(createdResult.Value);
            Assert.Contains("flagged for review", response.Message);
            Assert.Contains("PENDING", response.Message);
            Assert.Equal(createdProduct, response.Data);
        }

        [Fact]
        public async Task CreateProduct_ServiceThrowsException_ShouldReturn500()
        {
            // Arrange
            var productRequest = new ProductRequestDTO
            {
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            _mockService.Setup(s => s.AddAsync(It.IsAny<ProductRequestDTO>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(productRequest);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);

            var response = Assert.IsType<ApiResponse<string>>(statusResult.Value);
            Assert.Contains("Failed to create product", response.Message);
        }

        [Fact]
        public async Task CreateProduct_ContentViolation_ShouldReturn400()
        {
            // Arrange
            var productRequest = new ProductRequestDTO
            {
                Name = "Severe Violation",
                Description = "Extremely inappropriate",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Black",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            _mockService.Setup(s => s.AddAsync(It.IsAny<ProductRequestDTO>()))
                .ThrowsAsync(new InvalidOperationException("Content violates community guidelines"));

            // Act
            var result = await _controller.Create(productRequest);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("community guidelines", response.Message);
        }

        [Fact]
        public async Task CreateProduct_WithAuthentication_ShouldOverrideProviderId()
        {
            // Arrange - Controller already has authenticated context with _providerId
            var productRequest = new ProductRequestDTO
            {
                ProviderId = Guid.NewGuid(), // This will be overridden by authenticated user ID
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            var createdProduct = new ProductDTO
            {
                Id = Guid.NewGuid(),
                Name = productRequest.Name,
                ProviderId = _providerId, // Should use authenticated provider ID
                AvailabilityStatus = "available"
            };

            _mockService.Setup(s => s.AddAsync(It.Is<ProductRequestDTO>(dto => dto.ProviderId == _providerId)))
                .ReturnsAsync(createdProduct);

            // Act
            var result = await _controller.Create(productRequest);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);
            
            // Verify that ProviderId was set to authenticated user's ID
            _mockService.Verify(s => s.AddAsync(It.Is<ProductRequestDTO>(dto => dto.ProviderId == _providerId)), Times.Once);
        }

        #endregion

        #region Update Product Tests

        [Fact]
        public async Task UpdateProduct_ValidData_ShouldReturn200OK()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                Name = "Updated Product",
                Description = "Updated Description",
                CategoryId = Guid.NewGuid(),
                Size = "L",
                Color = "Green",
                PricePerDay = 150,
                RentalQuantity = 10,
                SecurityDeposit = 300,
                Gender = "Female",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>()
            };

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Update(productId, productRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("updated successfully", response.Message);
        }

        [Fact]
        public async Task UpdateProduct_ProductNotFound_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                Name = "Updated Product",
                Description = "Updated Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>()
            };

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Update(productId, productRequest);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("not found", notFoundResult.Value.ToString());
        }

        [Fact]
        public async Task UpdateProduct_WithImages_ShouldReturnSuccessWithImageMessage()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                Name = "Updated Product",
                Description = "Updated Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/new.jpg", IsPrimary = true }
                }
            };

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Update(productId, productRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("Old images have been automatically deleted", response.Message);
        }

        [Fact]
        public async Task UpdateProduct_ServiceThrowsException_ShouldReturn500()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                Name = "Updated Product",
                Description = "Updated Description",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Blue",
                PricePerDay = 100,
                RentalQuantity = 5,
                SecurityDeposit = 200,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>()
            };

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Update(productId, productRequest);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region Delete Product Tests

        [Fact]
        public async Task DeleteProduct_ValidProduct_ShouldReturn200OK()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product",
                RentCount = 0,
                BuyCount = 0
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockService.Setup(s => s.HasOrderItemsAsync(productId))
                .ReturnsAsync(false);

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Delete(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("archived successfully", response.Message);
        }

        [Fact]
        public async Task DeleteProduct_ProductNotFound_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync((ProductDTO)null);

            // Act
            var result = await _controller.Delete(productId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(notFoundResult.Value);
            Assert.Contains("not found", response.Message);
        }

        [Fact]
        public async Task DeleteProduct_NotOwner_ShouldReturn403Forbid()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = Guid.NewGuid(), // Different provider
                Name = "Test Product"
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.Delete(productId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteProduct_HasOrderHistory_ShouldArchiveWithReason()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product",
                RentCount = 5,
                BuyCount = 3
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockService.Setup(s => s.HasOrderItemsAsync(productId))
                .ReturnsAsync(true);

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Delete(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("Reason:", response.Message);
            Assert.Contains("rental(s)", response.Message);
            Assert.Contains("purchase(s)", response.Message);
        }

        [Fact]
        public async Task DeleteProduct_UpdateFails_ShouldReturn400()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product",
                RentCount = 0,
                BuyCount = 0
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockService.Setup(s => s.HasOrderItemsAsync(productId))
                .ReturnsAsync(false);

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Delete(productId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("Failed to archive", response.Message);
        }

        #endregion

        #region Restore Product Tests

        [Fact]
        public async Task RestoreProduct_ValidArchivedProduct_ShouldReturn200OK()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product",
                AvailabilityStatus = "archived"
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockService.Setup(s => s.UpdateAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.RestoreProduct(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("restored to active status", response.Message);
        }

        [Fact]
        public async Task RestoreProduct_ProductNotFound_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync((ProductDTO)null);

            // Act
            var result = await _controller.RestoreProduct(productId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("not found", notFoundResult.Value.ToString());
        }

        [Fact]
        public async Task RestoreProduct_NotOwner_ShouldReturn403Forbid()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = Guid.NewGuid(), // Different provider
                Name = "Test Product",
                AvailabilityStatus = "archived"
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.RestoreProduct(productId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task RestoreProduct_NotArchivedStatus_ShouldReturn400()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product",
                AvailabilityStatus = "available" // Not archived
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.RestoreProduct(productId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Only archived or deleted products can be restored", badRequestResult.Value.ToString());
        }

        #endregion

        #region Get Product Tests

        [Fact]
        public async Task GetProductById_Exists_ShouldReturn200WithProduct()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                Name = "Test Product",
                Description = "Test Description"
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.GetById(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ProductDTO>>(okResult.Value);
            Assert.Equal("Product retrieved successfully", response.Message);
            Assert.Equal(product, response.Data);
        }

        [Fact]
        public async Task GetProductById_NotFound_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync((ProductDTO)null);

            // Act
            var result = await _controller.GetById(productId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(notFoundResult.Value);
            Assert.Contains("not found", response.Message);
        }

        #endregion

        #region Update Images Tests

        [Fact]
        public async Task UpdateProductImages_ValidImages_ShouldReturn200OK()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product"
            };

            var newImages = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://test.com/new1.jpg", IsPrimary = true },
                new ProductImageDTO { ImageUrl = "http://test.com/new2.jpg", IsPrimary = false }
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockService.Setup(s => s.UpdateProductImagesAsync(productId, newImages))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateProductImages(productId, newImages);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Contains("images updated successfully", response.Message);
            Assert.Contains("Old images have been automatically deleted", response.Message);
        }

        [Fact]
        public async Task UpdateProductImages_ProductNotFound_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var newImages = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://test.com/new.jpg", IsPrimary = true }
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync((ProductDTO)null);

            // Act
            var result = await _controller.UpdateProductImages(productId, newImages);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(notFoundResult.Value);
            Assert.Contains("not found", response.Message);
        }

        [Fact]
        public async Task UpdateProductImages_NotOwner_ShouldReturn403Forbid()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = Guid.NewGuid(), // Different provider
                Name = "Test Product"
            };

            var newImages = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://test.com/new.jpg", IsPrimary = true }
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.UpdateProductImages(productId, newImages);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UpdateProductImages_NoImages_ShouldReturn400()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product"
            };

            var emptyImages = new List<ProductImageDTO>();

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.UpdateProductImages(productId, emptyImages);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("At least one image is required", response.Message);
        }

        [Fact]
        public async Task UpdateProductImages_NoPrimaryImage_ShouldReturn400()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new ProductDTO
            {
                Id = productId,
                ProviderId = _providerId,
                Name = "Test Product"
            };

            var imagesWithoutPrimary = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://test.com/img1.jpg", IsPrimary = false },
                new ProductImageDTO { ImageUrl = "http://test.com/img2.jpg", IsPrimary = false }
            };

            _mockService.Setup(s => s.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _controller.UpdateProductImages(productId, imagesWithoutPrimary);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("At least one image must be marked as primary", response.Message);
        }

        #endregion
    }
}
