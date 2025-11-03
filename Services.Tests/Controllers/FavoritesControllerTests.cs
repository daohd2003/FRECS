using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.FavoriteServices;
using ShareItAPI.Controllers;

namespace Services.Tests.Controllers
{
    /// <summary>
    /// Unit tests for FavoritesController (API Layer)
    /// Tests favorites functionality (view, add, remove) at controller level
    /// 
    /// How to run these tests:
    /// dotnet test --filter "FullyQualifiedName~FavoritesControllerTests"
    /// </summary>
    public class FavoritesControllerTests
    {
        private readonly Mock<IFavoriteService> _mockFavoriteService;
        private readonly FavoritesController _controller;

        public FavoritesControllerTests()
        {
            _mockFavoriteService = new Mock<IFavoriteService>();
            _controller = new FavoritesController(_mockFavoriteService.Object);
        }

        /// <summary>
        /// Get favorites - Valid user with favorites
        /// Expected: 200 OK with favorites list
        /// </summary>
        [Fact]
        public async Task GetFavorites_ValidUserWithFavorites_ShouldReturn200WithList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var favorites = new List<BusinessObject.Models.Favorite>
            {
                new BusinessObject.Models.Favorite
                {
                    UserId = userId,
                    ProductId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockFavoriteService.Setup(x => x.GetFavoritesByUserIdAsync(userId))
                .ReturnsAsync(favorites);

            // Act
            var result = await _controller.GetFavorites(userId, includeDetails: false);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Equal("Get favorites list successfully", apiResponse.Message);
            Assert.NotNull(apiResponse.Data);

            _mockFavoriteService.Verify(x => x.GetFavoritesByUserIdAsync(userId), Times.Once);
        }

        /// <summary>
        /// Get favorites with product details - Valid user
        /// Expected: 200 OK with detailed favorites list
        /// </summary>
        [Fact]
        public async Task GetFavorites_WithProductDetails_ShouldReturn200WithDetails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var favoritesWithDetails = new List<FavoriteWithProductDto>
            {
                new FavoriteWithProductDto
                {
                    UserId = userId,
                    ProductId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockFavoriteService.Setup(x => x.GetFavoritesWithProductDetailsAsync(userId))
                .ReturnsAsync(favoritesWithDetails);

            // Act
            var result = await _controller.GetFavorites(userId, includeDetails: true);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);

            _mockFavoriteService.Verify(x => x.GetFavoritesWithProductDetailsAsync(userId), Times.Once);
        }

        /// <summary>
        /// Check if favorite - Product is favorite
        /// Expected: 200 OK with true
        /// </summary>
        [Fact]
        public async Task IsFavorite_ProductIsFavorite_ShouldReturnTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteService.Setup(x => x.IsFavoriteAsync(userId, productId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.IsFavorite(userId, productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
            Assert.Equal("Favorite check successful", apiResponse.Message);
            Assert.True(apiResponse.Data);

            _mockFavoriteService.Verify(x => x.IsFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// Check if favorite - Product is not favorite
        /// Expected: 200 OK with false
        /// </summary>
        [Fact]
        public async Task IsFavorite_ProductIsNotFavorite_ShouldReturnFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteService.Setup(x => x.IsFavoriteAsync(userId, productId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.IsFavorite(userId, productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
            Assert.False(apiResponse.Data);
        }

        /// <summary>
        /// Add favorite - New favorite
        /// Expected: 200 OK with success message
        /// </summary>
        [Fact]
        public async Task AddFavorite_NewFavorite_ShouldReturn200()
        {
            // Arrange
            var dto = new FavoriteCreateDto
            {
                UserId = Guid.NewGuid(),
                ProductId = Guid.NewGuid()
            };

            _mockFavoriteService.Setup(x => x.IsFavoriteAsync(dto.UserId, dto.ProductId))
                .ReturnsAsync(false);

            _mockFavoriteService.Setup(x => x.AddFavoriteAsync(It.IsAny<BusinessObject.Models.Favorite>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.AddFavorite(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Added to favorites successfully", apiResponse.Message);

            _mockFavoriteService.Verify(x => x.AddFavoriteAsync(It.IsAny<BusinessObject.Models.Favorite>()), Times.Once);
        }

        /// <summary>
        /// Add favorite - Already exists
        /// Expected: 400 Bad Request
        /// </summary>
        [Fact]
        public async Task AddFavorite_AlreadyExists_ShouldReturn400()
        {
            // Arrange
            var dto = new FavoriteCreateDto
            {
                UserId = Guid.NewGuid(),
                ProductId = Guid.NewGuid()
            };

            _mockFavoriteService.Setup(x => x.IsFavoriteAsync(dto.UserId, dto.ProductId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.AddFavorite(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequestResult.Value);
            Assert.Contains("already in favorites", apiResponse.Message);

            // Verify AddFavoriteAsync was NOT called
            _mockFavoriteService.Verify(x => x.AddFavoriteAsync(It.IsAny<BusinessObject.Models.Favorite>()), Times.Never);
        }

        /// <summary>
        /// Remove favorite - Valid favorite
        /// Expected: 200 OK with success message
        /// </summary>
        [Fact]
        public async Task RemoveFavorite_ValidFavorite_ShouldReturn200()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            _mockFavoriteService.Setup(x => x.RemoveFavoriteAsync(userId, productId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RemoveFavorite(userId, productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
            Assert.Equal("Removed from favorites successfully", apiResponse.Message);

            _mockFavoriteService.Verify(x => x.RemoveFavoriteAsync(userId, productId), Times.Once);
        }

        /// <summary>
        /// Get favorites - Empty result
        /// Expected: 200 OK with empty list
        /// </summary>
        [Fact]
        public async Task GetFavorites_EmptyResult_ShouldReturn200WithEmptyList()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockFavoriteService.Setup(x => x.GetFavoritesByUserIdAsync(userId))
                .ReturnsAsync(new List<BusinessObject.Models.Favorite>());

            // Act
            var result = await _controller.GetFavorites(userId, includeDetails: false);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
        }
    }
}

