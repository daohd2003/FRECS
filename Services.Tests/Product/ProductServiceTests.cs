using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.ProductRepositories;
using Services.ProductServices;
using Services.ContentModeration;
using Services.EmailServices;
using Services.ConversationServices;

namespace Services.Tests.Product
{
    /// <summary>
    /// Unit tests for Product Service - View Product Details functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬─────────────────────────────────┬──────────────────────────────────┬──────────────────────┐
    /// │ Test ID │ Input GUID                      │ Expected Result                  │ Backend Log Level    │
    /// ├─────────┼─────────────────────────────────┼──────────────────────────────────┼──────────────────────┤
    /// │ UTCID01 │ Valid GUID, existing product    │ Return ProductDTO                │ -                    │
    /// │ UTCID02 │ Valid GUID, non-existent        │ Return null                      │ -                    │
    /// └─────────┴─────────────────────────────────┴──────────────────────────────────┴──────────────────────┘
    /// 
    /// Note: Service layer returns ProductDTO or null, no exceptions thrown.
    ///       FE Messages (not tested here):
    ///       - "Product not found."
    ///       - "The product you are looking for does not exist."
    ///       These are shown by Controller/FE when service returns null.
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Run specific test: dotnet test --filter "FullyQualifiedName~UTCID01"
    /// 3. Run all product tests: dotnet test --filter "FullyQualifiedName~ProductServiceTests"
    /// </summary>
    public class ProductServiceTests
    {
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IContentModerationService> _mockContentModerationService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConversationService> _mockConversationService;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockProductRepository = new Mock<IProductRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockContentModerationService = new Mock<IContentModerationService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockConversationService = new Mock<IConversationService>();

            _productService = new ProductService(
                _mockProductRepository.Object,
                _mockMapper.Object,
                _mockContentModerationService.Object,
                _mockServiceProvider.Object,
                _mockConversationService.Object
            );
        }

        /// <summary>
        /// UTCID01: Get product details with valid GUID for existing product
        /// Expected: Return ProductDTO with product details
        /// Backend: No exception, successful retrieval
        /// FE: Display product details page
        /// </summary>
        [Fact]
        public async Task UTCID01_GetByIdAsync_ExistingProduct_ShouldReturnProductDTO()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var providerId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var product = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = categoryId,
                PricePerDay = 100000,
                PurchasePrice = 500000,
                RentalQuantity = 5,
                PurchaseQuantity = 10,
                AvailabilityStatus = AvailabilityStatus.available,
                RentalStatus = RentalStatus.Available,
                PurchaseStatus = PurchaseStatus.Available,
                Gender = Gender.Unisex,
                SecurityDeposit = 200000,
                IsPromoted = false,
                RentCount = 10,
                BuyCount = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Images = new List<ProductImage>
                {
                    new ProductImage
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        ImageUrl = "https://example.com/image1.jpg",
                        IsPrimary = true
                    }
                }
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                ProviderName = "Test Provider",
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = categoryId,
                Category = "Test Category",
                PricePerDay = 100000,
                PurchasePrice = 500000,
                RentalQuantity = 5,
                PurchaseQuantity = 10,
                AvailabilityStatus = "Available",
                RentalStatus = "Available",
                PurchaseStatus = "Available",
                Gender = "Unisex",
                SecurityDeposit = 200000,
                IsPromoted = false,
                RentCount = 10,
                BuyCount = 5,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                PrimaryImagesUrl = "https://example.com/image1.jpg",
                AverageRating = 4.5m,
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO
                    {
                        Id = product.Images.First().Id,
                        ImageUrl = "https://example.com/image1.jpg",
                        IsPrimary = true
                    }
                }
            };

            _mockProductRepository.Setup(x => x.GetProductWithImagesByIdAsync(productId))
                .ReturnsAsync(product);

            _mockMapper.Setup(x => x.Map<ProductDTO>(product))
                .Returns(productDto);

            // Act
            var result = await _productService.GetByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);
            Assert.Equal("Test Product", result.Name);
            Assert.Equal("Test Description", result.Description);
            Assert.Equal(100000, result.PricePerDay);
            Assert.Equal(500000, result.PurchasePrice);
            Assert.Equal(5, result.RentalQuantity);
            Assert.Equal(10, result.PurchaseQuantity);
            Assert.Equal("Available", result.RentalStatus);
            Assert.Equal("Available", result.PurchaseStatus);
            Assert.True(result.IsRentalAvailable);
            Assert.True(result.IsPurchaseAvailable);
            Assert.NotNull(result.Images);
            Assert.Single(result.Images);

            // Verify repository was called
            _mockProductRepository.Verify(x => x.GetProductWithImagesByIdAsync(productId), Times.Once);

            // Verify mapper was called
            _mockMapper.Verify(x => x.Map<ProductDTO>(product), Times.Once);

            // FE will display product details successfully
        }

        /// <summary>
        /// UTCID02: Get product details with valid GUID for non-existent product
        /// Expected: Return null
        /// Backend: No exception, just return null
        /// FE Messages (NOT tested here):
        /// - "Product not found."
        /// - "The product you are looking for does not exist."
        /// Controller returns NotFound() when service returns null
        /// </summary>
        [Fact]
        public async Task UTCID02_GetByIdAsync_NonExistentProduct_ShouldReturnNull()
        {
            // Arrange
            var nonExistentProductId = Guid.NewGuid();

            _mockProductRepository.Setup(x => x.GetProductWithImagesByIdAsync(nonExistentProductId))
                .ReturnsAsync((BusinessObject.Models.Product?)null);

            // Act
            var result = await _productService.GetByIdAsync(nonExistentProductId);

            // Assert
            Assert.Null(result);

            // Verify repository was called
            _mockProductRepository.Verify(x => x.GetProductWithImagesByIdAsync(nonExistentProductId), Times.Once);

            // Verify mapper was NOT called (no product to map)
            _mockMapper.Verify(x => x.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()), Times.Never);

            // Controller will return NotFound()
            // FE will show: "Product not found." or "The product you are looking for does not exist."
        }

        /// <summary>
        /// Additional test: Verify product with only rental available
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_RentalOnlyProduct_ShouldReturnCorrectDTO()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new BusinessObject.Models.Product
            {
                Id = productId,
                Name = "Rental Only Product",
                RentalStatus = RentalStatus.Available,
                PurchaseStatus = PurchaseStatus.NotForSale,
                PricePerDay = 50000,
                RentalQuantity = 3,
                Images = new List<ProductImage>()
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Rental Only Product",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                PricePerDay = 50000,
                RentalQuantity = 3
            };

            _mockProductRepository.Setup(x => x.GetProductWithImagesByIdAsync(productId))
                .ReturnsAsync(product);

            _mockMapper.Setup(x => x.Map<ProductDTO>(product))
                .Returns(productDto);

            // Act
            var result = await _productService.GetByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Available", result.RentalStatus);
            Assert.Equal("Unavailable", result.PurchaseStatus);
            Assert.True(result.IsRentalAvailable);
            Assert.False(result.IsPurchaseAvailable);
        }

        /// <summary>
        /// Additional test: Verify product with only purchase available
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_PurchaseOnlyProduct_ShouldReturnCorrectDTO()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new BusinessObject.Models.Product
            {
                Id = productId,
                Name = "Purchase Only Product",
                RentalStatus = RentalStatus.NotAvailable,
                PurchaseStatus = PurchaseStatus.Available,
                PurchasePrice = 1000000,
                PurchaseQuantity = 20,
                Images = new List<ProductImage>()
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Purchase Only Product",
                RentalStatus = "Unavailable",
                PurchaseStatus = "Available",
                PurchasePrice = 1000000,
                PurchaseQuantity = 20
            };

            _mockProductRepository.Setup(x => x.GetProductWithImagesByIdAsync(productId))
                .ReturnsAsync(product);

            _mockMapper.Setup(x => x.Map<ProductDTO>(product))
                .Returns(productDto);

            // Act
            var result = await _productService.GetByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Unavailable", result.RentalStatus);
            Assert.Equal("Available", result.PurchaseStatus);
            Assert.False(result.IsRentalAvailable);
            Assert.True(result.IsPurchaseAvailable);
        }

        /// <summary>
        /// Additional test: Verify empty GUID returns null
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_EmptyGuid_ShouldReturnNull()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            _mockProductRepository.Setup(x => x.GetProductWithImagesByIdAsync(emptyGuid))
                .ReturnsAsync((BusinessObject.Models.Product?)null);

            // Act
            var result = await _productService.GetByIdAsync(emptyGuid);

            // Assert
            Assert.Null(result);

            _mockProductRepository.Verify(x => x.GetProductWithImagesByIdAsync(emptyGuid), Times.Once);
        }
    }
}

