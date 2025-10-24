using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.ProductServices;
using Services.ContentModeration;
using Services.ConversationServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for Product Controller (API Layer)
    /// Verifies API responses and HTTP status codes
    /// 
    /// Test Coverage:
    /// - Get Product By ID (GET /api/products/{id})
    /// - Get All Products (GET /api/products)
    /// 
    /// Note: ProductController does NOT use ApiResponse wrapper for GetById/GetAll
    ///       It returns ProductDTO or IQueryable<ProductDTO> directly
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~ProductControllerTests"
    /// </summary>
    public class ProductControllerTests
    {
        private readonly Mock<IProductService> _mockProductService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IContentModerationService> _mockContentModerationService;
        private readonly Mock<IConversationService> _mockConversationService;
        private readonly ProductController _controller;

        public ProductControllerTests()
        {
            _mockProductService = new Mock<IProductService>();
            _mockMapper = new Mock<IMapper>();
            _mockContentModerationService = new Mock<IContentModerationService>();
            _mockConversationService = new Mock<IConversationService>();
            _controller = new ProductController(
                _mockProductService.Object, 
                _mockMapper.Object,
                _mockContentModerationService.Object,
                _mockConversationService.Object
            );
        }

        #region GetById Tests

        /// <summary>
        /// Get Product By ID - Valid existing product
        /// Expected: 200 OK with ProductDTO
        /// Note: No ApiResponse wrapper, returns ProductDTO directly
        /// </summary>
        [Fact]
        public async Task GetById_ValidProductId_ShouldReturn200WithProduct()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Test Product",
                Description = "Test Description",
                PricePerDay = 100000,
                PurchasePrice = 500000,
                RentalQuantity = 5,
                PurchaseQuantity = 10,
                AvailabilityStatus = AvailabilityStatus.available.ToString(),
                RentalStatus = RentalStatus.Available.ToString(), // Will make ProductType = "BOTH"
                PurchaseStatus = PurchaseStatus.Available.ToString()
            };

            _mockProductService.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(productDto);

            // Act
            var result = await _controller.GetById(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var returnedProduct = Assert.IsType<ProductDTO>(okResult.Value);
            Assert.Equal(productId, returnedProduct.Id);
            Assert.Equal("Test Product", returnedProduct.Name);
            Assert.Equal("BOTH", returnedProduct.ProductType);

            _mockProductService.Verify(x => x.GetByIdAsync(productId), Times.Once);
        }

        /// <summary>
        /// Get Product By ID - Non-existent product
        /// Expected: 404 NotFound (no message)
        /// </summary>
        [Fact]
        public async Task GetById_NonExistentProductId_ShouldReturn404()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockProductService.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync((ProductDTO?)null);

            // Act
            var result = await _controller.GetById(productId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);

            _mockProductService.Verify(x => x.GetByIdAsync(productId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Get product with rental only
        /// </summary>
        [Fact]
        public async Task GetById_RentalOnlyProduct_ShouldReturnProductWithRentalType()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Rental Product",
                RentalStatus = RentalStatus.Available.ToString(), // Will make ProductType = "RENTAL"
                PurchaseStatus = PurchaseStatus.NotForSale.ToString(),
                PricePerDay = 50000,
                PurchasePrice = 0
            };

            _mockProductService.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(productDto);

            // Act
            var result = await _controller.GetById(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedProduct = Assert.IsType<ProductDTO>(okResult.Value);
            Assert.Equal("RENTAL", returnedProduct.ProductType);
        }

        #endregion

        #region GetAll Tests

        /// <summary>
        /// Get All Products - Has products
        /// Expected: 200 OK with IQueryable<ProductDTO>
        /// Note: No ApiResponse wrapper
        /// </summary>
        [Fact]
        public void GetAll_HasProducts_ShouldReturn200WithProductList()
        {
            // Arrange
            var products = new List<ProductDTO>
            {
                new ProductDTO
                {
                    Id = Guid.NewGuid(),
                    Name = "Product 1",
                    RentalStatus = RentalStatus.Available.ToString(), // BOTH type
                    PurchaseStatus = PurchaseStatus.Available.ToString()
                },
                new ProductDTO
                {
                    Id = Guid.NewGuid(),
                    Name = "Product 2",
                    RentalStatus = RentalStatus.Available.ToString(), // RENTAL type
                    PurchaseStatus = PurchaseStatus.NotForSale.ToString()
                }
            }.AsQueryable();

            _mockProductService.Setup(x => x.GetAll())
                .Returns(products);

            // Act
            var result = _controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var returnedProducts = Assert.IsAssignableFrom<IQueryable<ProductDTO>>(okResult.Value);
            Assert.Equal(2, returnedProducts.Count());

            _mockProductService.Verify(x => x.GetAll(), Times.Once);
        }

        /// <summary>
        /// Get All Products - No products
        /// Expected: 404 NotFound
        /// </summary>
        [Fact]
        public void GetAll_NoProducts_ShouldReturn404()
        {
            // Arrange
            _mockProductService.Setup(x => x.GetAll())
                .Returns((IQueryable<ProductDTO>?)null);

            // Act
            var result = _controller.GetAll();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);

            _mockProductService.Verify(x => x.GetAll(), Times.Once);
        }

        #endregion
    }
}

