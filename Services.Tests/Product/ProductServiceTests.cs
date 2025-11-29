using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using Moq;
using Repositories.ProductRepositories;
using Services.ProductServices;
using Services.ContentModeration;
using Services.EmailServices;
using Services.ConversationServices;
using Services.CloudServices;
using Services.NotificationServices;
using Product = BusinessObject.Models.Product;
using User = BusinessObject.Models.User;
using ProductImage = BusinessObject.Models.ProductImage;

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
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConversationService> _mockConversationService;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockProductRepository = new Mock<IProductRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockContentModerationService = new Mock<IContentModerationService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockConversationService = new Mock<IConversationService>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockNotificationService = new Mock<INotificationService>();

            _productService = new ProductService(
                _mockProductRepository.Object,
                _mockMapper.Object,
                _mockContentModerationService.Object,
                _mockServiceProvider.Object,
                _mockConversationService.Object,
                _mockCloudinaryService.Object,
                _mockNotificationService.Object
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

        #region Content Violation Notification Tests

        /// <summary>
        /// UTCID03: Create product with content violation
        /// Expected: Product created, notification sent to provider about violation
        /// Notification message should include: product name, violation reason, violated terms
        /// </summary>
        [Fact]
        public async Task UTCID03_CreateProduct_WithContentViolation_ShouldSendNotificationToProvider()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var createDto = new ProductRequestDTO
            {
                Name = "Inappropriate Product Name",
                Description = "This contains bad words",
                CategoryId = categoryId,
                PricePerDay = 100000,
                RentalQuantity = 5,
                Gender = "Unisex",
                SecurityDeposit = 200000
            };

            var newProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                CategoryId = categoryId,
                PricePerDay = createDto.PricePerDay,
                AvailabilityStatus = AvailabilityStatus.pending, // Set to pending due to violation
                CreatedAt = DateTime.UtcNow
            };

            var productWithProvider = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                Provider = new User
                {
                    Id = providerId,
                    Email = "provider@example.com",
                    Role = UserRole.provider
                }
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = false,
                Reason = "content policy violation",
                ViolatedTerms = new List<string> { "bad words", "inappropriate content" }
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description
            };

            _mockProductRepository.Setup(x => x.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(newProduct);

            _mockProductRepository.Setup(x => x.GetProductWithProviderByIdAsync(productId))
                .ReturnsAsync(productWithProvider);

            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(
                createDto.Name,
                createDto.Description))
                .ReturnsAsync(moderationResult);

            _mockProductRepository.Setup(x => x.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), It.IsAny<AvailabilityStatus>())).ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<ProductDTO>(newProduct))
                .Returns(productDto);

            // Act
            var result = await _productService.AddAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);

            // Verify notification was sent with correct parameters
            _mockNotificationService.Verify(x => x.SendNotification(
                providerId,
                It.Is<string>(msg => 
                    msg.Contains(createDto.Name) &&
                    msg.Contains("flagged for review") &&
                    msg.Contains("bad words, inappropriate content") &&
                    msg.Contains("content policy violation")),
                NotificationType.content_violation,
                null
            ), Times.Once);

            // Verify product status was set to pending
            _mockProductRepository.Verify(x => x.UpdateProductAvailabilityStatusAsync(
                productId,
                AvailabilityStatus.pending), Times.Once);
        }

        /// <summary>
        /// UTCID04: Create product without content violation
        /// Expected: Product created, NO notification sent
        /// </summary>
        [Fact]
        public async Task UTCID04_CreateProduct_WithoutContentViolation_ShouldNotSendNotification()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var createDto = new ProductRequestDTO
            {
                Name = "Clean Product Name",
                Description = "This is appropriate content",
                CategoryId = categoryId,
                PricePerDay = 100000,
                RentalQuantity = 5,
                Gender = "Unisex",
                SecurityDeposit = 200000
            };

            var newProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                CategoryId = categoryId,
                PricePerDay = createDto.PricePerDay,
                AvailabilityStatus = AvailabilityStatus.available,
                CreatedAt = DateTime.UtcNow
            };

            var productWithProvider = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                Provider = new User
                {
                    Id = providerId,
                    Email = "provider@example.com",
                    Role = UserRole.provider
                }
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = true,
                Reason = null,
                ViolatedTerms = new List<string>()
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description
            };

            _mockProductRepository.Setup(x => x.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(newProduct);

            _mockProductRepository.Setup(x => x.GetProductWithProviderByIdAsync(productId))
                .ReturnsAsync(productWithProvider);

            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(
                createDto.Name,
                createDto.Description))
                .ReturnsAsync(moderationResult);

            _mockMapper.Setup(x => x.Map<ProductDTO>(newProduct))
                .Returns(productDto);

            // Act
            var result = await _productService.AddAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);

            // Verify NO notification was sent
            _mockNotificationService.Verify(x => x.SendNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<Guid?>()
            ), Times.Never);

            // Verify product status was NOT changed to pending
            _mockProductRepository.Verify(x => x.UpdateProductAvailabilityStatusAsync(
                It.IsAny<Guid>(),
                AvailabilityStatus.pending), Times.Never);
        }

        /// <summary>
        /// UTCID05: Update product with content violation
        /// Expected: Product updated to pending status, notification sent to provider
        /// </summary>
        [Fact]
        public async Task UTCID05_UpdateProduct_WithContentViolation_ShouldSendNotificationToProvider()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var updateDto = new ProductDTO
            {
                Id = productId,
                Name = "Updated Product with Bad Content",
                Description = "This contains inappropriate words",
                CategoryId = categoryId,
                PricePerDay = 150000,
                Images = new List<ProductImageDTO>()
            };

            var existingProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Original Product Name",
                Description = "Original description",
                CategoryId = categoryId,
                PricePerDay = 100000,
                AvailabilityStatus = AvailabilityStatus.available,
                Images = new List<ProductImage>()
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = false,
                Reason = "inappropriate language detected",
                ViolatedTerms = new List<string> { "inappropriate words", "offensive content" }
            };

            _mockProductRepository.Setup(x => x.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync(existingProduct);

            _mockProductRepository.Setup(x => x.UpdateProductWithImagesAsync(updateDto))
                .ReturnsAsync(true);

            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(
                updateDto.Name,
                updateDto.Description))
                .ReturnsAsync(moderationResult);

            _mockProductRepository.Setup(x => x.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), It.IsAny<AvailabilityStatus>())).ReturnsAsync(true);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _productService.UpdateAsync(updateDto);

            // Assert
            Assert.True(result);

            // Verify notification was sent with correct parameters
            _mockNotificationService.Verify(x => x.SendNotification(
                providerId,
                It.Is<string>(msg => 
                    msg.Contains(updateDto.Name) &&
                    msg.Contains("flagged for review") &&
                    msg.Contains("inappropriate words, offensive content") &&
                    msg.Contains("inappropriate language detected")),
                NotificationType.content_violation,
                null
            ), Times.Once);

            // Verify product status was set to pending
            _mockProductRepository.Verify(x => x.UpdateProductAvailabilityStatusAsync(
                productId,
                AvailabilityStatus.pending), Times.Once);

            // Verify violation reason was saved
            _mockProductRepository.Verify(x => x.UpdateAsync(
                It.Is<BusinessObject.Models.Product>(p => 
                    p.Id == productId && 
                    p.ViolationReason == "inappropriate language detected")), Times.Once);
        }

        /// <summary>
        /// UTCID06: Update product without content violation (previously pending)
        /// Expected: Product status changed back to available, NO violation notification sent
        /// </summary>
        [Fact]
        public async Task UTCID06_UpdateProduct_WithoutViolation_PendingToAvailable_ShouldNotSendNotification()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var updateDto = new ProductDTO
            {
                Id = productId,
                Name = "Clean Updated Product",
                Description = "This is now appropriate content",
                CategoryId = categoryId,
                PricePerDay = 150000,
                Images = new List<ProductImageDTO>()
            };

            var existingProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Product with Bad Content",
                Description = "Bad description",
                CategoryId = categoryId,
                PricePerDay = 100000,
                AvailabilityStatus = AvailabilityStatus.pending, // Was pending due to violation
                ViolationReason = "Previous violation",
                Images = new List<ProductImage>()
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = true,
                Reason = null,
                ViolatedTerms = new List<string>()
            };

            _mockProductRepository.Setup(x => x.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync(existingProduct);

            _mockProductRepository.Setup(x => x.UpdateProductWithImagesAsync(updateDto))
                .ReturnsAsync(true);

            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(
                updateDto.Name,
                updateDto.Description))
                .ReturnsAsync(moderationResult);

            _mockProductRepository.Setup(x => x.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), It.IsAny<AvailabilityStatus>())).ReturnsAsync(true);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _productService.UpdateAsync(updateDto);

            // Assert
            Assert.True(result);

            // Verify NO violation notification was sent
            _mockNotificationService.Verify(x => x.SendNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                NotificationType.content_violation,
                It.IsAny<Guid?>()
            ), Times.Never);

            // Verify product status was changed back to available
            _mockProductRepository.Verify(x => x.UpdateProductAvailabilityStatusAsync(
                productId,
                AvailabilityStatus.available), Times.Once);

            // Verify violation reason was cleared
            _mockProductRepository.Verify(x => x.UpdateAsync(
                It.Is<BusinessObject.Models.Product>(p => 
                    p.Id == productId && 
                    p.ViolationReason == null)), Times.Once);
        }

        /// <summary>
        /// UTCID07: Create product with violation but notification fails
        /// Expected: Product still created with pending status, error logged but not thrown
        /// </summary>
        [Fact]
        public async Task UTCID07_CreateProduct_NotificationFails_ShouldStillCreateProduct()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var createDto = new ProductRequestDTO
            {
                Name = "Product with Violation",
                Description = "Bad content",
                CategoryId = categoryId,
                PricePerDay = 100000,
                RentalQuantity = 5,
                Gender = "Unisex",
                SecurityDeposit = 200000
            };

            var newProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                CategoryId = categoryId,
                PricePerDay = createDto.PricePerDay,
                AvailabilityStatus = AvailabilityStatus.pending,
                CreatedAt = DateTime.UtcNow
            };

            var productWithProvider = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                Provider = new User
                {
                    Id = providerId,
                    Email = "provider@example.com",
                    Role = UserRole.provider
                }
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = false,
                Reason = "violation detected",
                ViolatedTerms = new List<string> { "bad content" }
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description
            };

            _mockProductRepository.Setup(x => x.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(newProduct);

            _mockProductRepository.Setup(x => x.GetProductWithProviderByIdAsync(productId))
                .ReturnsAsync(productWithProvider);

            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(
                createDto.Name,
                createDto.Description))
                .ReturnsAsync(moderationResult);

            _mockProductRepository.Setup(x => x.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), It.IsAny<AvailabilityStatus>())).ReturnsAsync(true);

            // Notification service throws exception
            _mockNotificationService.Setup(x => x.SendNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<Guid?>()))
                .ThrowsAsync(new Exception("Notification service unavailable"));

            _mockMapper.Setup(x => x.Map<ProductDTO>(newProduct))
                .Returns(productDto);

            // Act
            var result = await _productService.AddAsync(createDto);

            // Assert - Product should still be created despite notification failure
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);

            // Verify notification was attempted
            _mockNotificationService.Verify(x => x.SendNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                NotificationType.content_violation,
                null
            ), Times.Once);

            // Verify product status was still set to pending
            _mockProductRepository.Verify(x => x.UpdateProductAvailabilityStatusAsync(
                productId,
                AvailabilityStatus.pending), Times.Once);
        }

        /// <summary>
        /// UTCID08: Verify notification message format for content violation
        /// Expected: Message contains product name, reason, and violated terms
        /// </summary>
        [Fact]
        public async Task UTCID08_ContentViolationNotification_ShouldHaveCorrectMessageFormat()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var productName = "Test Product";

            var createDto = new ProductRequestDTO
            {
                Name = productName,
                Description = "Violation content",
                CategoryId = categoryId,
                PricePerDay = 100000,
                RentalQuantity = 5,
                Gender = "Unisex",
                SecurityDeposit = 200000
            };

            var newProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Description = createDto.Description,
                CategoryId = categoryId,
                AvailabilityStatus = AvailabilityStatus.pending,
                CreatedAt = DateTime.UtcNow
            };

            var productWithProvider = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = createDto.Name,
                Provider = new User { Id = providerId, Email = "provider@example.com" }
            };

            var moderationResult = new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
            {
                IsAppropriate = false,
                Reason = "offensive language",
                ViolatedTerms = new List<string> { "term1", "term2", "term3" }
            };

            var productDto = new ProductDTO { Id = productId, Name = productName };

            _mockProductRepository.Setup(x => x.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(newProduct);
            _mockProductRepository.Setup(x => x.GetProductWithProviderByIdAsync(productId))
                .ReturnsAsync(productWithProvider);
            _mockContentModerationService.Setup(x => x.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(moderationResult);
            _mockProductRepository.Setup(x => x.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), It.IsAny<AvailabilityStatus>()))
                .ReturnsAsync(true);
            _mockMapper.Setup(x => x.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(productDto);

            string capturedMessage = string.Empty;
            _mockNotificationService.Setup(x => x.SendNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<Guid?>()))
                .Callback<Guid, string, NotificationType, Guid?>((userId, message, type, orderId) =>
                {
                    capturedMessage = message;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _productService.AddAsync(createDto);

            // Assert - Verify message format
            Assert.Contains(productName, capturedMessage);
            Assert.Contains("flagged for review", capturedMessage);
            Assert.Contains("offensive language", capturedMessage);
            Assert.Contains("term1, term2, term3", capturedMessage);
            Assert.Contains("Please update your product to comply with our guidelines", capturedMessage);
        }

        #endregion
    }
}




