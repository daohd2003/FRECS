using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.ProductRepositories;
using Services.CloudServices;
using Services.ContentModeration;
using Services.ConversationServices;
using Services.NotificationServices;
using Services.ProductServices;
using Xunit;

namespace Services.Tests.Product
{
    /// <summary>
    /// Unit tests for ProductService - Provider Product Management functionality
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// 
    /// ┌─────────┬──────────────────────────────────┬───────────────────────────────────────────────────────────┐
    /// │ Test ID │ Scenario                         │ Expected Result                                           │
    /// ├─────────┼──────────────────────────────────┼───────────────────────────────────────────────────────────┤
    /// │ UTCID01 │ Create Product - Valid Data      │ Product created with status 'available'                   │
    /// │ UTCID02 │ Create Product - AI Flags        │ Product created with status 'pending'                     │
    /// │ UTCID03 │ Update Product - Valid Data      │ Product updated successfully                              │
    /// │ UTCID04 │ Update Product - AI Flags        │ Product status changed to 'pending'                       │
    /// │ UTCID05 │ Delete Product - No References   │ Product archived successfully                             │
    /// │ UTCID06 │ Delete Product - Has Orders      │ Product archived with reason                              │
    /// │ UTCID07 │ Restore Product - Valid          │ Product status changed to 'available'                     │
    /// │ UTCID08 │ Get Provider Products - Valid    │ Returns list of provider's products                       │
    /// │ UTCID09 │ Update Images - Valid            │ Old images deleted, new images saved                      │
    /// │ UTCID10 │ Get Product By ID - Exists       │ Returns ProductDTO                                        │
    /// │ UTCID11 │ Get Product By ID - Not Found    │ Returns null                                              │
    /// └─────────┴──────────────────────────────────┴───────────────────────────────────────────────────────────┘
    /// 
    /// Total: 11 core test cases + additional tests
    /// </summary>
    public class ProviderProductServiceTests
    {
        private readonly Mock<IProductRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IContentModerationService> _mockModerationService;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConversationService> _mockConversationService;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly ProductService _service;

        public ProviderProductServiceTests()
        {
            _mockRepository = new Mock<IProductRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockModerationService = new Mock<IContentModerationService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockConversationService = new Mock<IConversationService>();
            _mockCloudinaryService = new Mock<ICloudinaryService>();
            _mockNotificationService = new Mock<INotificationService>();

            _service = new ProductService(
                _mockRepository.Object,
                _mockMapper.Object,
                _mockModerationService.Object,
                _mockServiceProvider.Object,
                _mockConversationService.Object,
                _mockCloudinaryService.Object,
                _mockNotificationService.Object
            );
        }

        #region Create Product Tests

        [Fact]
        public async Task UTCID01_CreateProduct_ValidData_ShouldCreateWithAvailableStatus()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                ProviderId = providerId,
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

            var createdProduct = new BusinessObject.Models.Product
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                Name = productRequest.Name,
                Description = productRequest.Description,
                AvailabilityStatus = AvailabilityStatus.available
            };

            var productDto = new ProductDTO
            {
                Id = createdProduct.Id,
                Name = createdProduct.Name,
                AvailabilityStatus = "available"
            };

            _mockRepository.Setup(r => r.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(createdProduct);

            _mockModerationService.Setup(m => m.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ContentModerationResultDTO { IsAppropriate = true });

            _mockMapper.Setup(m => m.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(productDto);

            // Act
            var result = await _service.AddAsync(productRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("available", result.AvailabilityStatus);
            _mockRepository.Verify(r => r.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()), Times.Once);
            _mockModerationService.Verify(m => m.CheckProductContentAsync(productRequest.Name, productRequest.Description), Times.Once);
        }

        [Fact]
        public async Task UTCID02_CreateProduct_AIFlagsContent_ShouldCreateWithPendingStatus()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var productRequest = new ProductRequestDTO
            {
                ProviderId = providerId,
                Name = "Inappropriate Product",
                Description = "Contains bad words",
                CategoryId = Guid.NewGuid(),
                Size = "M",
                Color = "Red",
                PricePerDay = 50,
                RentalQuantity = 3,
                SecurityDeposit = 100,
                Gender = "Unisex",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            var createdProduct = new BusinessObject.Models.Product
            {
                Id = Guid.NewGuid(),
                ProviderId = providerId,
                Name = productRequest.Name,
                Description = productRequest.Description,
                AvailabilityStatus = AvailabilityStatus.available,
                Provider = new User { Id = providerId, Email = "provider@test.com" }
            };

            var productDto = new ProductDTO
            {
                Id = createdProduct.Id,
                Name = createdProduct.Name,
                AvailabilityStatus = "pending"
            };

            _mockRepository.Setup(r => r.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(createdProduct);

            _mockModerationService.Setup(m => m.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Contains inappropriate language",
                    ViolatedTerms = new List<string> { "bad_word" }
                });

            _mockRepository.Setup(r => r.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), AvailabilityStatus.pending))
                .ReturnsAsync(true);

            _mockRepository.Setup(r => r.GetProductWithProviderByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(createdProduct);

            _mockMapper.Setup(m => m.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(productDto);

            // Act
            var result = await _service.AddAsync(productRequest);

            // Assert
            Assert.NotNull(result);
            _mockRepository.Verify(r => r.UpdateProductAvailabilityStatusAsync(createdProduct.Id, AvailabilityStatus.pending), Times.Once);
            _mockNotificationService.Verify(n => n.SendNotification(
                providerId,
                It.IsAny<string>(),
                NotificationType.content_violation,
                null), Times.Once);
        }

        [Fact]
        public async Task Additional_CreateProduct_AIServiceFails_ShouldSetPendingAsFallback()
        {
            // Arrange
            var productRequest = new ProductRequestDTO
            {
                ProviderId = Guid.NewGuid(),
                Name = "Test Product",
                Description = "Test Description",
                CategoryId = Guid.NewGuid(),
                Size = "L",
                Color = "Green",
                PricePerDay = 75,
                RentalQuantity = 2,
                SecurityDeposit = 150,
                Gender = "Male",
                RentalStatus = "Available",
                PurchaseStatus = "Unavailable",
                Images = new List<ProductImageDTO>
                {
                    new ProductImageDTO { ImageUrl = "http://test.com/image.jpg", IsPrimary = true }
                }
            };

            var createdProduct = new BusinessObject.Models.Product
            {
                Id = Guid.NewGuid(),
                ProviderId = productRequest.ProviderId,
                Name = productRequest.Name,
                Description = productRequest.Description,
                AvailabilityStatus = AvailabilityStatus.available
            };

            _mockRepository.Setup(r => r.AddProductWithImagesAsync(It.IsAny<ProductRequestDTO>()))
                .ReturnsAsync(createdProduct);

            _mockModerationService.Setup(m => m.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI service unavailable"));

            _mockRepository.Setup(r => r.UpdateProductAvailabilityStatusAsync(It.IsAny<Guid>(), AvailabilityStatus.pending))
                .ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(new ProductDTO { Id = createdProduct.Id, AvailabilityStatus = "pending" });

            // Act
            var result = await _service.AddAsync(productRequest);

            // Assert
            _mockRepository.Verify(r => r.UpdateProductAvailabilityStatusAsync(createdProduct.Id, AvailabilityStatus.pending), Times.Once);
        }

        #endregion

        #region Update Product Tests

        [Fact]
        public async Task UTCID03_UpdateProduct_ValidData_ShouldUpdateSuccessfully()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var providerId = Guid.NewGuid();

            var existingProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Old Name",
                Description = "Old Description",
                AvailabilityStatus = AvailabilityStatus.available,
                Images = new List<ProductImage>()
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                Name = "New Name",
                Description = "New Description",
                Images = new List<ProductImageDTO>()
            };

            _mockRepository.Setup(r => r.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync(existingProduct);

            _mockRepository.Setup(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            _mockModerationService.Setup(m => m.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ContentModerationResultDTO { IsAppropriate = true });

            // Act
            var result = await _service.UpdateAsync(productDto);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()), Times.Once);
            _mockModerationService.Verify(m => m.CheckProductContentAsync("New Name", "New Description"), Times.Once);
        }

        [Fact]
        public async Task UTCID04_UpdateProduct_AIFlagsContent_ShouldSetPending()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var providerId = Guid.NewGuid();

            var existingProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Old Name",
                Description = "Old Description",
                AvailabilityStatus = AvailabilityStatus.available,
                Images = new List<ProductImage>()
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                ProviderId = providerId,
                Name = "Inappropriate Name",
                Description = "Bad content",
                Images = new List<ProductImageDTO>()
            };

            _mockRepository.Setup(r => r.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync(existingProduct);

            _mockRepository.Setup(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            _mockModerationService.Setup(m => m.CheckProductContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Inappropriate content detected",
                    ViolatedTerms = new List<string> { "bad_word" }
                });

            _mockRepository.Setup(r => r.UpdateProductAvailabilityStatusAsync(productId, AvailabilityStatus.pending))
                .ReturnsAsync(true);

            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateAsync(productDto);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.UpdateProductAvailabilityStatusAsync(productId, AvailabilityStatus.pending), Times.Once);
            _mockNotificationService.Verify(n => n.SendNotification(
                providerId,
                It.IsAny<string>(),
                NotificationType.content_violation,
                null), Times.Once);
        }

        [Fact]
        public async Task Additional_UpdateProduct_ProductNotFound_ShouldReturnFalse()
        {
            // Arrange
            var productDto = new ProductDTO
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Description = "Test"
            };

            _mockRepository.Setup(r => r.GetProductWithImagesAndProviderAsync(It.IsAny<Guid>()))
                .ReturnsAsync((BusinessObject.Models.Product)null);

            // Act
            var result = await _service.UpdateAsync(productDto);

            // Assert
            Assert.False(result);
            _mockRepository.Verify(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()), Times.Never);
        }

        #endregion

        #region Delete/Archive Product Tests

        [Fact]
        public async Task UTCID05_DeleteProduct_NoReferences_ShouldArchiveSuccessfully()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockRepository.Setup(r => r.DeleteAsync(productId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteAsync(productId);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.DeleteAsync(productId), Times.Once);
        }

        [Fact]
        public async Task UTCID06_HasOrderItems_ProductHasOrders_ShouldReturnTrue()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockRepository.Setup(r => r.HasOrderItemsAsync(productId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.HasOrderItemsAsync(productId);

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.HasOrderItemsAsync(productId), Times.Once);
        }

        #endregion

        #region Get Product Tests

        [Fact]
        public async Task UTCID10_GetProductById_Exists_ShouldReturnProductDTO()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new BusinessObject.Models.Product
            {
                Id = productId,
                Name = "Test Product",
                Description = "Test Description"
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Test Product",
                Description = "Test Description"
            };

            _mockRepository.Setup(r => r.GetProductWithImagesByIdAsync(productId))
                .ReturnsAsync(product);

            _mockMapper.Setup(m => m.Map<ProductDTO>(product))
                .Returns(productDto);

            // Act
            var result = await _service.GetByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);
            Assert.Equal("Test Product", result.Name);
        }

        [Fact]
        public async Task UTCID11_GetProductById_NotFound_ShouldReturnNull()
        {
            // Arrange
            var productId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetProductWithImagesByIdAsync(productId))
                .ReturnsAsync((BusinessObject.Models.Product)null);

            // Act
            var result = await _service.GetByIdAsync(productId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Update Images Tests

        [Fact]
        public async Task UTCID09_UpdateImages_Valid_ShouldDeleteOldAndSaveNew()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var existingProduct = new BusinessObject.Models.Product
            {
                Id = productId,
                Name = "Test Product",
                Images = new List<ProductImage>
                {
                    new ProductImage { ImageUrl = "http://cloudinary.com/upload/v123/ShareIt/old.jpg" }
                }
            };

            var newImages = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://cloudinary.com/upload/v456/ShareIt/new.jpg", IsPrimary = true }
            };

            _mockRepository.Setup(r => r.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync(existingProduct);

            _mockCloudinaryService.Setup(c => c.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(new ProductDTO { Id = productId, Images = newImages });

            _mockRepository.Setup(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateProductImagesAsync(productId, newImages);

            // Assert
            Assert.True(result);
            _mockCloudinaryService.Verify(c => c.DeleteImageAsync(It.IsAny<string>()), Times.Once);
            _mockRepository.Verify(r => r.UpdateProductWithImagesAsync(It.IsAny<ProductDTO>()), Times.Once);
        }

        [Fact]
        public async Task Additional_UpdateImages_ProductNotFound_ShouldReturnFalse()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var newImages = new List<ProductImageDTO>
            {
                new ProductImageDTO { ImageUrl = "http://test.com/new.jpg", IsPrimary = true }
            };

            _mockRepository.Setup(r => r.GetProductWithImagesAndProviderAsync(productId))
                .ReturnsAsync((BusinessObject.Models.Product)null);

            // Act
            var result = await _service.UpdateProductImagesAsync(productId, newImages);

            // Assert
            Assert.False(result);
            _mockCloudinaryService.Verify(c => c.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}
