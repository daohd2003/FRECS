using AutoMapper;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Models;
using Moq;
using Repositories.FavoriteRepositories;
using Services.FavoriteServices;

namespace Services.Tests.Favorite
{
    /// <summary>
    /// Unit tests for FavoriteService - View, add, and remove favorites functionality
    /// 
    /// Test Coverage Matrix:
    /// ┌─────────┬──────────────────────────────────┬───────────────────────────────────────────────────────────┐
    /// │ Test ID │ Scenario                         │ Expected Result                                           │
    /// ├─────────┼──────────────────────────────────┼───────────────────────────────────────────────────────────┤
    /// │ UTCID01 │ View favorites valid user        │ Return list of favorites                                  │
    /// │ UTCID02 │ View favorites no favorites      │ Return empty list                                         │
    /// │ UTCID03 │ Add to favorites valid product   │ Successfully add to favorites                             │
    /// │ UTCID04 │ Add duplicate favorite           │ Check returns true (already favorite)                     │
    /// │ UTCID05 │ Remove from favorites valid      │ Successfully remove from favorites                        │
    /// │ UTCID06 │ Check if favorite exists         │ Return true if exists, false otherwise                    │
    /// │ UTCID07 │ Get favorites with product details│ Return favorites with full product information           │
    /// └─────────┴──────────────────────────────────┴───────────────────────────────────────────────────────────┘
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~FavoriteServiceTests"
    /// </summary>
    public class FavoriteServiceTests
    {
        private readonly Mock<IFavoriteRepository> _mockFavoriteRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly FavoriteService _favoriteService;

        public FavoriteServiceTests()
        {
            _mockFavoriteRepo = new Mock<IFavoriteRepository>();
            _mockMapper = new Mock<IMapper>();
            _favoriteService = new FavoriteService(_mockFavoriteRepo.Object, _mockMapper.Object);
        }

        /// <summary>
        /// UTCID01: View favorites for valid user with favorites
        /// Expected: Return list of favorite products
        /// </summary>
        [Fact]
        public async Task UTCID01_GetFavoritesByUserId_ValidUserWithFavorites_ShouldReturnList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var favorites = new List<BusinessObject.Models.Favorite>
            {
                new BusinessObject.Models.Favorite
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockFavoriteRepo.Setup(x => x.GetFavoritesByUserIdAsync(userId))
                .ReturnsAsync(favorites);

            // Act
            var result = await _favoriteService.GetFavoritesByUserIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(userId, result[0].UserId);
            Assert.Equal(productId, result[0].ProductId);

            _mockFavoriteRepo.Verify(x => x.GetFavoritesByUserIdAsync(userId), Times.Once);
        }

        /// <summary>
        /// UTCID02: View favorites for user with no favorites
        /// Expected: Return empty list
        /// </summary>
        [Fact]
        public async Task UTCID02_GetFavoritesByUserId_UserWithNoFavorites_ShouldReturnEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.GetFavoritesByUserIdAsync(userId))
                .ReturnsAsync(new List<BusinessObject.Models.Favorite>());

            // Act
            var result = await _favoriteService.GetFavoritesByUserIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _mockFavoriteRepo.Verify(x => x.GetFavoritesByUserIdAsync(userId), Times.Once);
        }

        /// <summary>
        /// UTCID03: Add to favorites with valid product
        /// Expected: Successfully add product to favorites
        /// </summary>
        [Fact]
        public async Task UTCID03_AddFavorite_ValidProduct_ShouldAddSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var favorite = new BusinessObject.Models.Favorite
            {
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            _mockFavoriteRepo.Setup(x => x.AddFavoriteAsync(It.IsAny<BusinessObject.Models.Favorite>()))
                .Returns(Task.CompletedTask);

            // Act
            await _favoriteService.AddFavoriteAsync(favorite);

            // Assert
            _mockFavoriteRepo.Verify(x => x.AddFavoriteAsync(It.Is<BusinessObject.Models.Favorite>(
                f => f.UserId == userId && f.ProductId == productId)), Times.Once);
        }

        /// <summary>
        /// UTCID04: Check if product is already in favorites (duplicate check)
        /// Expected: Return true if already favorite
        /// </summary>
        [Fact]
        public async Task UTCID04_IsFavorite_AlreadyFavorite_ShouldReturnTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.IsFavoriteAsync(userId, productId))
                .ReturnsAsync(true);

            // Act
            var result = await _favoriteService.IsFavoriteAsync(userId, productId);

            // Assert
            Assert.True(result);

            _mockFavoriteRepo.Verify(x => x.IsFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// UTCID05: Remove from favorites
        /// Expected: Successfully remove product from favorites
        /// </summary>
        [Fact]
        public async Task UTCID05_RemoveFavorite_ValidFavorite_ShouldRemoveSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.RemoveFavoriteAsync(userId, productId))
                .Returns(Task.CompletedTask);

            // Act
            await _favoriteService.RemoveFavoriteAsync(userId, productId);

            // Assert
            _mockFavoriteRepo.Verify(x => x.RemoveFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// UTCID06: Check if favorite exists
        /// Expected: Return true if exists, false otherwise
        /// </summary>
        [Fact]
        public async Task UTCID06_IsFavorite_NotFavorite_ShouldReturnFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.IsFavoriteAsync(userId, productId))
                .ReturnsAsync(false);

            // Act
            var result = await _favoriteService.IsFavoriteAsync(userId, productId);

            // Assert
            Assert.False(result);

            _mockFavoriteRepo.Verify(x => x.IsFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// UTCID07: Get favorites with product details
        /// Expected: Return favorites with full product information including images
        /// </summary>
        [Fact]
        public async Task UTCID07_GetFavoritesWithProductDetails_ValidUser_ShouldReturnWithDetails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var favorites = new List<BusinessObject.Models.Favorite>
            {
                new BusinessObject.Models.Favorite
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAt = DateTime.UtcNow,
                    Product = new BusinessObject.Models.Product
                    {
                        Id = productId,
                        Name = "Test Product",
                        Description = "Test Description",
                        CategoryId = categoryId,
                        Category = new Category { Id = categoryId, Name = "Test Category" },
                        Images = new List<ProductImage>
                        {
                            new ProductImage
                            {
                                Id = Guid.NewGuid(),
                                ImageUrl = "test.jpg",
                                IsPrimary = true
                            }
                        }
                    }
                }
            };

            var productDto = new ProductDTO
            {
                Id = productId,
                Name = "Test Product",
                Description = "Test Description",
                Category = "Test Category",
                PrimaryImagesUrl = "test.jpg"
            };

            _mockFavoriteRepo.Setup(x => x.GetFavoritesWithProductDetailsAsync(userId))
                .ReturnsAsync(favorites);

            _mockMapper.Setup(x => x.Map<ProductDTO>(It.IsAny<BusinessObject.Models.Product>()))
                .Returns(productDto);

            // Act
            var result = await _favoriteService.GetFavoritesWithProductDetailsAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(userId, result[0].UserId);
            Assert.Equal(productId, result[0].ProductId);
            Assert.NotNull(result[0].Product);
            Assert.Equal("Test Product", result[0].Product.Name);
            Assert.Equal("Test Category", result[0].Product.Category);
            Assert.Equal("test.jpg", result[0].Product.PrimaryImagesUrl);

            _mockFavoriteRepo.Verify(x => x.GetFavoritesWithProductDetailsAsync(userId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Add favorite verifies all properties
        /// </summary>
        [Fact]
        public async Task AddFavorite_ShouldSetAllProperties()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            var favorite = new BusinessObject.Models.Favorite
            {
                UserId = userId,
                ProductId = productId,
                CreatedAt = createdAt
            };

            _mockFavoriteRepo.Setup(x => x.AddFavoriteAsync(It.IsAny<BusinessObject.Models.Favorite>()))
                .Returns(Task.CompletedTask);

            // Act
            await _favoriteService.AddFavoriteAsync(favorite);

            // Assert
            _mockFavoriteRepo.Verify(x => x.AddFavoriteAsync(It.Is<BusinessObject.Models.Favorite>(
                f => f.UserId == userId && 
                     f.ProductId == productId && 
                     f.CreatedAt == createdAt)), Times.Once);
        }

        /// <summary>
        /// Additional Test: Get favorites with multiple products
        /// </summary>
        [Fact]
        public async Task GetFavoritesByUserId_MultipleProducts_ShouldReturnAllFavorites()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var product1Id = Guid.NewGuid();
            var product2Id = Guid.NewGuid();

            var favorites = new List<BusinessObject.Models.Favorite>
            {
                new BusinessObject.Models.Favorite
                {
                    UserId = userId,
                    ProductId = product1Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new BusinessObject.Models.Favorite
                {
                    UserId = userId,
                    ProductId = product2Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            _mockFavoriteRepo.Setup(x => x.GetFavoritesByUserIdAsync(userId))
                .ReturnsAsync(favorites);

            // Act
            var result = await _favoriteService.GetFavoritesByUserIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, f => f.ProductId == product1Id);
            Assert.Contains(result, f => f.ProductId == product2Id);
        }

        /// <summary>
        /// Additional Test: Remove favorite for non-existent favorite (should not throw)
        /// </summary>
        [Fact]
        public async Task RemoveFavorite_NonExistent_ShouldNotThrow()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.RemoveFavoriteAsync(userId, productId))
                .Returns(Task.CompletedTask);

            // Act & Assert - should not throw
            await _favoriteService.RemoveFavoriteAsync(userId, productId);

            _mockFavoriteRepo.Verify(x => x.RemoveFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// Additional Test: Get favorites with product details - empty result
        /// </summary>
        [Fact]
        public async Task GetFavoritesWithProductDetails_NoFavorites_ShouldReturnEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockFavoriteRepo.Setup(x => x.GetFavoritesWithProductDetailsAsync(userId))
                .ReturnsAsync(new List<BusinessObject.Models.Favorite>());

            // Act
            var result = await _favoriteService.GetFavoritesWithProductDetailsAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _mockFavoriteRepo.Verify(x => x.GetFavoritesWithProductDetailsAsync(userId), Times.Once);
        }
    }
}

