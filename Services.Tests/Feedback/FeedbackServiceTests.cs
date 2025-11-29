using System.Threading.Tasks;
using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using Moq;
using Repositories.FeedbackRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Services.FeedbackServices;
using Profile = BusinessObject.Models.Profile;
using User = BusinessObject.Models.User;
using Order = BusinessObject.Models.Order;
using OrderItem = BusinessObject.Models.OrderItem;
using ProductImage = BusinessObject.Models.ProductImage;
using Feedback = BusinessObject.Models.Feedback;

namespace Services.Tests.Feedback
{
    /// <summary>
    /// Unit tests for Feedback Service - View Feedback functionality
    /// Test cases based on the provided test matrix
    /// 
    /// Test Coverage Matrix (Backend Service Layer):
    /// ┌─────────┬──────────────────────────────────┬───────────────────────┬──────────────────────────────────┐
    /// │ Test ID │ Product GUID                     │ Page Number           │ Expected Result                  │
    /// ├─────────┼──────────────────────────────────┼───────────────────────┼──────────────────────────────────┤
    /// │ UTCID01 │ Valid, has feedback              │ Valid (e.g., 1)       │ Success with feedback list       │
    /// │ UTCID02 │ Valid, has feedback              │ Out of range (e.g., 999) │ Success with empty list       │
    /// │ UTCID03 │ Valid, no feedback               │ Valid (e.g., 1)       │ Success with empty list          │
    /// │ UTCID04 │ Invalid/non-existent             │ Valid (e.g., 1)       │ Success with empty list          │
    /// └─────────┴──────────────────────────────────┴───────────────────────┴──────────────────────────────────┘
    /// 
    /// Note: Service layer returns ApiResponse<PaginatedResponse<FeedbackResponseDto>>, no exceptions.
    ///       All cases return success, difference is in Items count (empty or populated).
    ///       No backend-specific log messages - service just returns data.
    /// 
    /// How to run these tests:
    /// 1. Command line: dotnet test Services.Tests/Services.Tests.csproj
    /// 2. Run specific test: dotnet test --filter "FullyQualifiedName~UTCID01"
    /// 3. Run all feedback tests: dotnet test --filter "FullyQualifiedName~FeedbackServiceTests"
    /// </summary>
    public class FeedbackServiceTests
    {
        private readonly Mock<IFeedbackRepository> _mockFeedbackRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IRepository<OrderItem>> _mockOrderItemRepository;
        private readonly Mock<IRepository<BusinessObject.Models.Product>> _mockProductRepository;
        private readonly Mock<IRepository<User>> _mockUserRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<Services.ContentModeration.IContentModerationService> _mockContentModerationService;
        private readonly FeedbackService _feedbackService;

        public FeedbackServiceTests()
        {
            _mockFeedbackRepository = new Mock<IFeedbackRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockOrderItemRepository = new Mock<IRepository<OrderItem>>();
            _mockProductRepository = new Mock<IRepository<BusinessObject.Models.Product>>();
            _mockUserRepository = new Mock<IRepository<User>>();
            _mockMapper = new Mock<IMapper>();
            _mockContentModerationService = new Mock<Services.ContentModeration.IContentModerationService>();

            // Setup default behavior: no violations
            _mockContentModerationService
                .Setup(x => x.CheckContentAsync(It.IsAny<string>()))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });

            _feedbackService = new FeedbackService(
                _mockFeedbackRepository.Object,
                _mockOrderRepository.Object,
                _mockOrderItemRepository.Object,
                _mockProductRepository.Object,
                _mockUserRepository.Object,
                _mockMapper.Object,
                _mockContentModerationService.Object
            );
        }

        /// <summary>
        /// UTCID01: Valid GUID for product with feedback + Page number within valid range
        /// Expected: Success with feedback list
        /// Backend: Returns ApiResponse with data
        /// FE: Display feedback list
        /// </summary>
        [Fact]
        public async Task UTCID01_GetFeedbacksByProduct_ValidProductWithFeedback_ShouldReturnPaginatedList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            int page = 1;
            int pageSize = 10;

            var feedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    ProductId = productId,
                    CustomerId = customerId,
                    Rating = 5,
                    Comment = "Great product!",
                    CreatedAt = DateTime.UtcNow,
                    Customer = new User
                    {
                        Id = customerId,
                        Profile = new BusinessObject.Models.Profile { FullName = "Test Customer" }
                    }
                },
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    ProductId = productId,
                    CustomerId = customerId,
                    Rating = 4,
                    Comment = "Good product",
                    CreatedAt = DateTime.UtcNow,
                    Customer = new User
                    {
                        Id = customerId,
                        Profile = new BusinessObject.Models.Profile { FullName = "Test Customer 2" }
                    }
                }
            };

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = page,
                PageSize = pageSize,
                TotalItems = 2
            };

            var feedbackDtos = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = feedbacks[0].Id,
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    CustomerName = "Test Customer",
                    Rating = 5,
                    Comment = "Great product!",
                    SubmittedAt = feedbacks[0].CreatedAt
                },
                new FeedbackResponseDto
                {
                    FeedbackId = feedbacks[1].Id,
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    CustomerName = "Test Customer 2",
                    Rating = 4,
                    Comment = "Good product",
                    SubmittedAt = feedbacks[1].CreatedAt
                }
            };

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(paginatedFeedbacks);

            _mockMapper.Setup(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, page, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Items.Count);
            Assert.Equal(page, result.Data.Page);
            Assert.Equal(pageSize, result.Data.PageSize);
            Assert.Equal(2, result.Data.TotalItems);
            Assert.Equal(5, result.Data.Items[0].Rating);
            Assert.Equal("Great product!", result.Data.Items[0].Comment);

            // Verify repository was called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize), Times.Once);

            // Verify mapper was called
            _mockMapper.Verify(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items), Times.Once);
        }

        /// <summary>
        /// UTCID02: Valid GUID for product with feedback + Page number outside valid range
        /// Expected: Success with empty list (no items on that page)
        /// Backend: Returns ApiResponse with empty Items
        /// FE: Display "No feedback found" or empty state
        /// </summary>
        [Fact]
        public async Task UTCID02_GetFeedbacksByProduct_PageOutOfRange_ShouldReturnEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            int page = 999; // Page out of range
            int pageSize = 10;

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = new List<BusinessObject.Models.Feedback>(), // Empty list
                Page = page,
                PageSize = pageSize,
                TotalItems = 5 // Total items exist but not on this page
            };

            var emptyFeedbackDtos = new List<FeedbackResponseDto>();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(paginatedFeedbacks);

            _mockMapper.Setup(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items))
                .Returns(emptyFeedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, page, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Items);
            Assert.Equal(page, result.Data.Page);
            Assert.Equal(0, result.Data.TotalItems); // Filtered count is 0

            // Verify repository was called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize), Times.Once);
        }

        /// <summary>
        /// UTCID03: Valid GUID for product with no feedback + Page number within valid range
        /// Expected: Success with empty list
        /// Backend: Returns ApiResponse with empty Items and TotalItems = 0
        /// FE: Display "No feedback yet" or empty state
        /// </summary>
        [Fact]
        public async Task UTCID03_GetFeedbacksByProduct_ProductWithNoFeedback_ShouldReturnEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            int page = 1;
            int pageSize = 10;

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = new List<BusinessObject.Models.Feedback>(), // Empty list
                Page = page,
                PageSize = pageSize,
                TotalItems = 0 // No feedback at all
            };

            var emptyFeedbackDtos = new List<FeedbackResponseDto>();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(paginatedFeedbacks);

            _mockMapper.Setup(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items))
                .Returns(emptyFeedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, page, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Items);
            Assert.Equal(0, result.Data.TotalItems);
            Assert.Equal(page, result.Data.Page);

            // Verify repository was called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(productId, page, pageSize), Times.Once);
        }

        /// <summary>
        /// UTCID04: Invalid/non-existent GUID + Page number within valid range
        /// Expected: Success with empty list (repository returns empty for non-existent product)
        /// Backend: Returns ApiResponse with empty Items
        /// FE: Display "No feedback found" or could show "Product not found"
        /// Note: Service doesn't validate if product exists, just queries feedback
        /// </summary>
        [Fact]
        public async Task UTCID04_GetFeedbacksByProduct_NonExistentProduct_ShouldReturnEmptyList()
        {
            // Arrange
            var nonExistentProductId = Guid.NewGuid();
            int page = 1;
            int pageSize = 10;

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = new List<BusinessObject.Models.Feedback>(), // Empty list
                Page = page,
                PageSize = pageSize,
                TotalItems = 0
            };

            var emptyFeedbackDtos = new List<FeedbackResponseDto>();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAsync(nonExistentProductId, page, pageSize))
                .ReturnsAsync(paginatedFeedbacks);

            _mockMapper.Setup(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items))
                .Returns(emptyFeedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(nonExistentProductId, page, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Items);
            Assert.Equal(0, result.Data.TotalItems);

            // Verify repository was called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(nonExistentProductId, page, pageSize), Times.Once);

            // Note: Service doesn't check if product exists - just returns empty feedback list
        }

        /// <summary>
        /// Additional test: Invalid page number (less than 1)
        /// Expected: Returns error message
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_InvalidPageNumber_ShouldReturnErrorMessage()
        {
            // Arrange
            var productId = Guid.NewGuid();
            int invalidPage = 0;
            int pageSize = 10;

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, invalidPage, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Invalid page or pageSize", result.Message);
            Assert.Null(result.Data);

            // Verify repository was NOT called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            ), Times.Never);
        }

        /// <summary>
        /// Additional test: Invalid page size (less than 1)
        /// Expected: Returns error message
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_InvalidPageSize_ShouldReturnErrorMessage()
        {
            // Arrange
            var productId = Guid.NewGuid();
            int page = 1;
            int invalidPageSize = 0;

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, page, invalidPageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Invalid page or pageSize", result.Message);
            Assert.Null(result.Data);

            // Verify repository was NOT called
            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>()
            ), Times.Never);
        }

        /// <summary>
        /// Additional test: Verify pagination calculations
        /// </summary>
        [Fact]
        public async Task GetFeedbacksByProduct_MultipleFeedbacks_ShouldReturnCorrectPagination()
        {
            // Arrange
            var productId = Guid.NewGuid();
            int page = 2;
            int pageSize = 5;
            int totalItems = 12;

            var feedbacksPage2 = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Rating = 3,
                    Comment = "Feedback 6"
                },
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Rating = 4,
                    Comment = "Feedback 7"
                }
            };

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacksPage2,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            var feedbackDtos = feedbacksPage2.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.Id,
                Rating = f.Rating,
                Comment = f.Comment
            }).ToList();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAsync(productId, page, pageSize))
                .ReturnsAsync(paginatedFeedbacks);

            _mockMapper.Setup(x => x.Map<List<FeedbackResponseDto>>(paginatedFeedbacks.Items))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAsync(productId, page, pageSize, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Equal(2, result.Data.Items.Count); // Page 2 has 2 items (items 6-7 out of 12)
            Assert.Equal(page, result.Data.Page);
            Assert.Equal(pageSize, result.Data.PageSize);
            Assert.Equal(2, result.Data.TotalItems); // Filtered count
        }
        #region GetFeedbacksByProductAndCustomerAsync Tests (Provider Role - View Customer Comments)

        /// <summary>
        /// UTCID01: Provider views customer feedback for specific product and customer
        /// Expected: Return feedback list mapped from repository
        /// Use case: Provider viewing customer comments in Order Detail page
        /// </summary>
        [Fact]
        public async Task UTCID01_GetFeedbacksByProductAndCustomer_ShouldReturnMappedFeedbacks()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var feedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    CustomerId = customerId,
                    Rating = 5,
                    Comment = "Great product!",
                    TargetType = FeedbackTargetType.Product,
                    CreatedAt = DateTime.UtcNow
                }
            };

            var feedbackDtos = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = feedbacks[0].Id,
                    TargetType = FeedbackTargetType.Product,
                    TargetId = productId,
                    CustomerId = customerId,
                    Rating = 5,
                    Comment = "Great product!",
                    SubmittedAt = feedbacks[0].CreatedAt
                }
            };

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(feedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(feedbacks))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAndCustomerAsync(productId, customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Great product!", result.First().Comment);
            Assert.Equal(customerId, result.First().CustomerId);
            Assert.Equal(productId, result.First().TargetId);

            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId), Times.Once);
            _mockMapper.Verify(x => x.Map<IEnumerable<FeedbackResponseDto>>(feedbacks), Times.Once);
        }

        /// <summary>
        /// UTCID02: Provider views customer feedback - no feedback exists
        /// Expected: Return empty list
        /// </summary>
        [Fact]
        public async Task UTCID02_GetFeedbacksByProductAndCustomer_NoFeedback_ShouldReturnEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var emptyFeedbacks = new List<BusinessObject.Models.Feedback>();
            var emptyDtos = new List<FeedbackResponseDto>();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(emptyFeedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(emptyFeedbacks))
                .Returns(emptyDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAndCustomerAsync(productId, customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// UTCID03: Provider views customer feedback - multiple feedbacks
        /// Expected: Return all feedbacks for that product and customer
        /// </summary>
        [Fact]
        public async Task UTCID03_GetFeedbacksByProductAndCustomer_MultipleFeedbacks_ShouldReturnAllFeedbacks()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var feedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    CustomerId = customerId,
                    Rating = 5,
                    Comment = "First feedback",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    CustomerId = customerId,
                    Rating = 4,
                    Comment = "Second feedback",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            var feedbackDtos = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.Id,
                Rating = f.Rating,
                Comment = f.Comment
            }).ToList();

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProductAndCustomerAsync(productId, customerId))
                .ReturnsAsync(feedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(feedbacks))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProductAndCustomerAsync(productId, customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        #endregion

        #region GetFeedbacksByProviderIdAsync Tests (Provider Role - View All Customer Comments)

        /// <summary>
        /// UTCID04: Provider views all feedbacks for their products/orders
        /// Expected: Return mapped feedback list from repository
        /// </summary>
        [Fact]
        public async Task UTCID04_GetFeedbacksByProviderId_ProviderOwnFeedbacks_ShouldReturnMappedFeedbacks()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var providerUser = new User
            {
                Id = providerId,
                Role = UserRole.provider
            };

            var feedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    CustomerId = Guid.NewGuid(),
                    Rating = 5,
                    Comment = "Excellent product!",
                    TargetType = FeedbackTargetType.Product,
                    CreatedAt = DateTime.UtcNow
                }
            };

            var feedbackDtos = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = feedbacks[0].Id,
                    Rating = 5,
                    Comment = "Excellent product!"
                }
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(providerId))
                .ReturnsAsync(providerUser);
            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId))
                .ReturnsAsync(feedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(feedbacks))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProviderIdAsync(providerId, providerId, false);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Excellent product!", result.First().Comment);

            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProviderIdAsync(providerId), Times.Once);
        }

        /// <summary>
        /// UTCID05: Provider views feedbacks - no feedbacks exist
        /// Expected: Return empty list
        /// </summary>
        [Fact]
        public async Task UTCID05_GetFeedbacksByProviderId_NoFeedbacks_ShouldReturnEmptyList()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var providerUser = new User
            {
                Id = providerId,
                Role = UserRole.provider
            };
            var emptyFeedbacks = new List<BusinessObject.Models.Feedback>();
            var emptyDtos = new List<FeedbackResponseDto>();

            _mockUserRepository.Setup(x => x.GetByIdAsync(providerId))
                .ReturnsAsync(providerUser);
            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId))
                .ReturnsAsync(emptyFeedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(emptyFeedbacks))
                .Returns(emptyDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProviderIdAsync(providerId, providerId, false);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// UTCID06: Provider tries to view another provider's feedbacks
        /// Expected: Throw UnauthorizedAccessException
        /// </summary>
        [Fact]
        public async Task UTCID06_GetFeedbacksByProviderId_DifferentProvider_ShouldThrowUnauthorized()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var differentProviderId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _feedbackService.GetFeedbacksByProviderIdAsync(differentProviderId, providerId, false));

            _mockFeedbackRepository.Verify(x => x.GetFeedbacksByProviderIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID07: Provider views feedbacks with provider response
        /// Expected: Return feedbacks including provider responses
        /// </summary>
        [Fact]
        public async Task UTCID07_GetFeedbacksByProviderId_WithProviderResponse_ShouldReturnFeedbacksWithResponses()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var providerUser = new User
            {
                Id = providerId,
                Role = UserRole.provider
            };

            var feedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    CustomerId = Guid.NewGuid(),
                    Rating = 5,
                    Comment = "Great product!",
                    ProviderResponse = "Thank you for your feedback!",
                    ProviderResponseAt = DateTime.UtcNow,
                    ProviderResponseById = providerId,
                    CreatedAt = DateTime.UtcNow
                }
            };

            var feedbackDtos = new List<FeedbackResponseDto>
            {
                new FeedbackResponseDto
                {
                    FeedbackId = feedbacks[0].Id,
                    Rating = 5,
                    Comment = "Great product!",
                    ProviderResponse = "Thank you for your feedback!",
                    ProviderResponseAt = feedbacks[0].ProviderResponseAt
                }
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(providerId))
                .ReturnsAsync(providerUser);
            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByProviderIdAsync(providerId))
                .ReturnsAsync(feedbacks);
            _mockMapper.Setup(x => x.Map<IEnumerable<FeedbackResponseDto>>(feedbacks))
                .Returns(feedbackDtos);

            // Act
            var result = await _feedbackService.GetFeedbacksByProviderIdAsync(providerId, providerId, false);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.NotNull(result.First().ProviderResponse);
            Assert.Equal("Thank you for your feedback!", result.First().ProviderResponse);
        }

        /// <summary>
        /// UTCID08: Invalid provider ID (not a provider)
        /// Expected: Throw ArgumentException
        /// </summary>
        [Fact]
        public async Task UTCID08_GetFeedbacksByProviderId_InvalidProviderId_ShouldThrowArgumentException()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var customerUser = new User
            {
                Id = providerId,
                Role = UserRole.customer // Not a provider
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(providerId))
                .ReturnsAsync(customerUser);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _feedbackService.GetFeedbacksByProviderIdAsync(providerId, providerId, false));
        }

        #endregion

        #region GetAllFeedbacksAsync Tests (View All Product Feedback - Staff/Admin)

        /// <summary>
        /// Helper method to create sample feedbacks for GetAllFeedbacksAsync tests
        /// </summary>
        private List<BusinessObject.Models.Feedback> CreateSampleFeedbacksForManagement()
        {
            var customerId1 = Guid.NewGuid();
            var customerId2 = Guid.NewGuid();
            var productId1 = Guid.NewGuid();
            var productId2 = Guid.NewGuid();
            var providerId = Guid.NewGuid();

            return new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    ProductId = productId1,
                    CustomerId = customerId1,
                    Rating = 5,
                    Comment = "Excellent product!",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    IsBlocked = false,
                    IsVisible = true,
                    ProviderResponse = "Thank you!",
                    ProviderResponseAt = DateTime.UtcNow,
                    Customer = new User
                    {
                        Id = customerId1,
                        Email = "customer1@test.com",
                        Profile = new Profile { FullName = "John Doe", ProfilePictureUrl = "pic1.jpg" }
                    },
                    Product = new BusinessObject.Models.Product {
                        Id = productId1,
                        Name = "Laptop Dell",
                        PricePerDay = 100,
                        ProviderId = providerId,
                        Provider = new User { Profile = new Profile { FullName = "Provider One" } },
                        Images = new List<ProductImage> { new ProductImage { ImageUrl = "laptop.jpg" } }
                    },
                    ProviderResponder = new User { Profile = new Profile { FullName = "Provider One" } }
                },
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    ProductId = productId2,
                    CustomerId = customerId2,
                    Rating = 3,
                    Comment = "Average quality",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    IsBlocked = false,
                    IsVisible = true,
                    Customer = new User
                    {
                        Id = customerId2,
                        Email = "customer2@test.com",
                        Profile = new Profile { FullName = "Jane Smith", ProfilePictureUrl = "pic2.jpg" }
                    },
                    Product = new BusinessObject.Models.Product {
                        Id = productId2,
                        Name = "Camera Canon",
                        PricePerDay = 50,
                        ProviderId = providerId,
                        Provider = new User { Profile = new Profile { FullName = "Provider Two" } },
                        Images = new List<ProductImage> { new ProductImage { ImageUrl = "camera.jpg" } }
                    }
                },
                new BusinessObject.Models.Feedback
                {
                    Id = Guid.NewGuid(),
                    TargetType = FeedbackTargetType.Product,
                    ProductId = productId1,
                    CustomerId = customerId2,
                    Rating = 1,
                    Comment = "Bad product",
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    IsBlocked = true,
                    IsVisible = false,
                    BlockedAt = DateTime.UtcNow.AddDays(-29),
                    Customer = new User
                    {
                        Id = customerId2,
                        Email = "customer2@test.com",
                        Profile = new Profile { FullName = "Jane Smith" }
                    },
                    Product = new BusinessObject.Models.Product {
                        Id = productId1,
                        Name = "Laptop Dell",
                        PricePerDay = 100,
                        ProviderId = providerId,
                        Provider = new User { Profile = new Profile { FullName = "Provider One" } },
                        Images = new List<ProductImage>()
                    },
                    BlockedBy = new User { Profile = new Profile { FullName = "Admin User" } }
                }
            };
        }

        /// <summary>
        /// Test: Get all feedbacks without any filters
        /// Expected: Returns all feedbacks with "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_NoFilters_ShouldReturnAllFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 3
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(3, result.Data.Items.Count);
            Assert.Equal(3, result.Data.TotalItems);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Filter by search term (customer name)
        /// Expected: Returns feedbacks matching customer name with "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_FilterByCustomerName_ShouldReturnMatchingFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { SearchTerm = "John", PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().Where(f => f.Customer.Profile.FullName.Contains("John")).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 1
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Single(result.Data.Items);
            Assert.Contains("John", result.Data.Items[0].CustomerName);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Filter by search term (product name)
        /// Expected: Returns feedbacks for matching product with "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_FilterByProductName_ShouldReturnMatchingFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { SearchTerm = "Laptop", PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().Where(f => f.Product.Name.Contains("Laptop")).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 2
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Equal(2, result.Data.Items.Count);
            Assert.All(result.Data.Items, item => Assert.Contains("Laptop", item.ProductName));

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Filter by rating
        /// Expected: Returns only feedbacks with specified rating and "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_FilterByRating_ShouldReturnMatchingFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { Rating = 5, PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().Where(f => f.Rating == 5).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 1
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Single(result.Data.Items);
            Assert.Equal(5, result.Data.Items[0].Rating);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Filter by response status - Responded
        /// Expected: Returns only feedbacks with provider response and "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_FilterByRespondedStatus_ShouldReturnRespondedFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { ResponseStatus = "Responded", PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().Where(f => f.ProviderResponse != null).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 1
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Single(result.Data.Items);
            Assert.NotNull(result.Data.Items[0].ProviderResponse);
            Assert.Equal("Responded", result.Data.Items[0].Status);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Filter by blocked status - true
        /// Expected: Returns only blocked feedbacks and "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_FilterByBlockedTrue_ShouldReturnBlockedFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { IsBlocked = true, PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().Where(f => f.IsBlocked).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 1
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Single(result.Data.Items);
            Assert.True(result.Data.Items[0].IsBlocked);
            Assert.Equal("Blocked", result.Data.Items[0].Status);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Sort by Rating ascending
        /// Expected: Returns feedbacks sorted by lowest rating first and "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_SortByRatingAsc_ShouldReturnSortedFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto { SortBy = "Rating", SortOrder = "asc", PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement().OrderBy(f => f.Rating).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 3
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Equal(3, result.Data.Items.Count);
            Assert.Equal(1, result.Data.Items[0].Rating);
            Assert.Equal(5, result.Data.Items[2].Rating);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Pagination
        /// Expected: Returns correct page with "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_Pagination_ShouldReturnCorrectPage()
        {
            // Arrange
            var filter = new FeedbackFilterDto { PageNumber = 1, PageSize = 2 };
            var allFeedbacks = CreateSampleFeedbacksForManagement();
            var feedbacksPage1 = allFeedbacks.Take(2).ToList();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacksPage1,
                Page = 1,
                PageSize = 2,
                TotalItems = 3
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Equal(2, result.Data.Items.Count);
            Assert.Equal(1, result.Data.Page);
            Assert.Equal(2, result.Data.PageSize);
            Assert.Equal(3, result.Data.TotalItems);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Multiple filters combined
        /// Expected: Returns feedbacks matching all criteria and "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_MultipleFilters_ShouldReturnMatchingFeedbacks()
        {
            // Arrange
            var filter = new FeedbackFilterDto
            {
                SearchTerm = "Laptop",
                Rating = 5,
                ResponseStatus = "Responded",
                IsBlocked = false,
                PageNumber = 1,
                PageSize = 10
            };

            var feedbacks = CreateSampleFeedbacksForManagement()
                .Where(f => f.Product.Name.Contains("Laptop")
                    && f.Rating == 5
                    && f.ProviderResponse != null
                    && !f.IsBlocked)
                .ToList();

            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 1
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Single(result.Data.Items);
            Assert.Contains("Laptop", result.Data.Items[0].ProductName);
            Assert.Equal(5, result.Data.Items[0].Rating);
            Assert.NotNull(result.Data.Items[0].ProviderResponse);
            Assert.False(result.Data.Items[0].IsBlocked);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: No feedbacks match filter criteria
        /// Expected: Returns empty list with "Success" message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_NoMatchingFeedbacks_ShouldReturnEmptyList()
        {
            // Arrange
            var filter = new FeedbackFilterDto { SearchTerm = "NonExistent", PageNumber = 1, PageSize = 10 };
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = new List<BusinessObject.Models.Feedback>(),
                Page = 1,
                PageSize = 10,
                TotalItems = 0
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);
            Assert.Empty(result.Data.Items);
            Assert.Equal(0, result.Data.TotalItems);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Repository throws exception
        /// Expected: Returns error response with exception message
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_RepositoryThrowsException_ShouldReturnErrorResponse()
        {
            // Arrange
            var filter = new FeedbackFilterDto { PageNumber = 1, PageSize = 10 };
            var exceptionMessage = "Database connection failed";

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal($"Error: {exceptionMessage}", result.Message);
            Assert.Null(result.Data);

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        /// <summary>
        /// Test: Verify status field calculation
        /// Expected: Status is correctly set based on feedback state
        /// </summary>
        [Fact]
        public async Task GetAllFeedbacks_VerifyStatusCalculation_ShouldSetCorrectStatus()
        {
            // Arrange
            var filter = new FeedbackFilterDto { PageNumber = 1, PageSize = 10 };
            var feedbacks = CreateSampleFeedbacksForManagement();
            var paginatedFeedbacks = new PaginatedResponse<BusinessObject.Models.Feedback>
            {
                Items = feedbacks,
                Page = 1,
                PageSize = 10,
                TotalItems = 3
            };

            _mockFeedbackRepository.Setup(x => x.GetAllFeedbacksWithFilterAsync(filter))
                .ReturnsAsync(paginatedFeedbacks);

            // Act
            var result = await _feedbackService.GetAllFeedbacksAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Success", result.Message);

            var blockedFeedback = result.Data.Items.FirstOrDefault(f => f.IsBlocked);
            if (blockedFeedback != null)
            {
                Assert.Equal("Blocked", blockedFeedback.Status);
            }

            var respondedFeedback = result.Data.Items.FirstOrDefault(f => f.ProviderResponse != null && !f.IsBlocked);
            if (respondedFeedback != null)
            {
                Assert.Equal("Responded", respondedFeedback.Status);
            }

            var activeFeedback = result.Data.Items.FirstOrDefault(f => f.ProviderResponse == null && !f.IsBlocked);
            if (activeFeedback != null)
            {
                Assert.Equal("Active", activeFeedback.Status);
            }

            _mockFeedbackRepository.Verify(x => x.GetAllFeedbacksWithFilterAsync(filter), Times.Once);
        }

        #endregion

        #region BlockFeedbackAsync Tests (Block Product Feedback - Staff/Admin)

        /// <summary>
        /// Test: Block feedback successfully
        /// Expected: Returns success response with "Feedback blocked successfully" message
        /// </summary>
        [Fact]
        public async Task BlockFeedback_ValidFeedbackId_ShouldBlockSuccessfully()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var feedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "Test feedback",
                IsBlocked = false,
                IsVisible = true
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(feedback);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { feedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId, AverageRating = 5.0m, RatingCount = 1 });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(feedbackId), Times.Once);
        }

        /// <summary>
        /// Test: Block non-existent feedback
        /// Expected: Returns error response with "Feedback not found" message
        /// </summary>
        [Fact]
        public async Task BlockFeedback_NonExistentFeedback_ShouldReturnNotFound()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(false);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback not found", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Test: Block feedback and recalculate product rating
        /// Expected: Product rating is recalculated after blocking
        /// </summary>
        [Fact]
        public async Task BlockFeedback_WithProductFeedback_ShouldRecalculateProductRating()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var blockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 1,
                Comment = "Bad feedback",
                IsBlocked = true,
                IsVisible = false
            };

            var remainingFeedbacks = new List<BusinessObject.Models.Feedback>
            {
                new BusinessObject.Models.Feedback { Id = Guid.NewGuid(), ProductId = productId, Rating = 5 },
                new BusinessObject.Models.Feedback { Id = Guid.NewGuid(), ProductId = productId, Rating = 4 }
            };

            var product = new BusinessObject.Models.Product {
                Id = productId,
                AverageRating = 3.33m,
                RatingCount = 3
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(blockedFeedback);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(remainingFeedbacks);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify product rating was recalculated
            _mockProductRepository.Verify(x => x.GetByIdAsync(productId), Times.Once);
            _mockProductRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Product>(p =>
                p.Id == productId &&
                p.AverageRating == 4.5m && // (5 + 4) / 2
                p.RatingCount == 2
            )), Times.Once);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
        }

        /// <summary>
        /// Test: Block feedback without product (Order feedback)
        /// Expected: Blocks successfully without recalculating product rating
        /// </summary>
        [Fact]
        public async Task BlockFeedback_OrderFeedback_ShouldBlockWithoutRecalculation()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var orderFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = null, // Order feedback, no product
                OrderId = Guid.NewGuid(),
                Rating = 3,
                Comment = "Order feedback",
                IsBlocked = true,
                IsVisible = false
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(orderFeedback);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(feedbackId), Times.Once);
            // Should not try to recalculate product rating
            _mockProductRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockProductRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()), Times.Never);
        }

        /// <summary>
        /// Test: Repository throws exception during block
        /// Expected: Returns error response with exception message
        /// </summary>
        [Fact]
        public async Task BlockFeedback_RepositoryThrowsException_ShouldReturnErrorResponse()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var exceptionMessage = "Database error";

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal($"Error: {exceptionMessage}", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
        }

        /// <summary>
        /// Test: Block already blocked feedback
        /// Expected: Still returns success (idempotent operation)
        /// </summary>
        [Fact]
        public async Task BlockFeedback_AlreadyBlocked_ShouldReturnSuccess()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var alreadyBlockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 1,
                Comment = "Already blocked",
                IsBlocked = true,
                IsVisible = false,
                BlockedAt = DateTime.UtcNow.AddDays(-1),
                BlockedById = Guid.NewGuid()
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(alreadyBlockedFeedback);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback>());

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId, AverageRating = 0m, RatingCount = 0 });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
        }

        /// <summary>
        /// Test: Block feedback sets correct properties
        /// Expected: IsBlocked=true, IsVisible=false, BlockedAt and BlockedById are set
        /// Note: This is tested at repository level, service just calls repository
        /// </summary>
        [Fact]
        public async Task BlockFeedback_ShouldSetCorrectProperties()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var feedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                Rating = 3,
                Comment = "Test",
                IsBlocked = false,
                IsVisible = true
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(feedback);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify repository method was called with correct parameters
            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
        }

        /// <summary>
        /// Test: Block feedback with empty GUID
        /// Expected: Returns "Feedback not found" message
        /// </summary>
        [Fact]
        public async Task BlockFeedback_EmptyGuid_ShouldReturnNotFound()
        {
            // Arrange
            var feedbackId = Guid.Empty;
            var staffId = Guid.NewGuid();

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(false);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback not found", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.BlockFeedbackAsync(feedbackId, staffId), Times.Once);
        }

        /// <summary>
        /// Test: Block feedback recalculates rating to 0 when all feedbacks are blocked
        /// Expected: Product rating becomes 0 and rating count becomes 0
        /// </summary>
        [Fact]
        public async Task BlockFeedback_LastFeedback_ShouldSetRatingToZero()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var lastFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "Last feedback",
                IsBlocked = true,
                IsVisible = false
            };

            var product = new BusinessObject.Models.Product {
                Id = productId,
                AverageRating = 5.0m,
                RatingCount = 1
            };

            _mockFeedbackRepository.Setup(x => x.BlockFeedbackAsync(feedbackId, staffId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(lastFeedback);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback>()); // No feedbacks left

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.BlockFeedbackAsync(feedbackId, staffId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback blocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify product rating was set to 0
            _mockProductRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Product>(p =>
                p.Id == productId &&
                p.AverageRating == 0.0m &&
                p.RatingCount == 0
            )), Times.Once);
        }

        #endregion

        #region UnblockFeedbackAsync Tests (Unblock Product Feedback - Staff/Admin)

        /// <summary>
        /// Test: Unblock feedback successfully
        /// Expected: Returns success response with "Feedback unblocked successfully" message
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_ValidFeedbackId_ShouldUnblockSuccessfully()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var blockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "Test feedback",
                IsBlocked = true,
                IsVisible = false,
                ViolationReason = "Inappropriate content"
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(blockedFeedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { blockedFeedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId, AverageRating = 0m, RatingCount = 0 });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(feedbackId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.Id == feedbackId &&
                f.IsVisible == true &&
                f.ViolationReason == null
            )), Times.Once);
        }

        /// <summary>
        /// Test: Unblock non-existent feedback
        /// Expected: Returns error response with "Feedback not found" message
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_NonExistentFeedback_ShouldReturnNotFound()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(false);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback not found", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// Test: Unblock feedback and recalculate product rating
        /// Expected: Product rating is recalculated after unblocking
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_WithProductFeedback_ShouldRecalculateProductRating()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var unblockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "Good feedback",
                IsBlocked = false,
                IsVisible = true
            };

            var existingFeedbacks = new List<BusinessObject.Models.Feedback>
            {
                unblockedFeedback,
                new BusinessObject.Models.Feedback { Id = Guid.NewGuid(), ProductId = productId, Rating = 3 },
                new BusinessObject.Models.Feedback { Id = Guid.NewGuid(), ProductId = productId, Rating = 4 }
            };

            var product = new BusinessObject.Models.Product {
                Id = productId,
                AverageRating = 3.5m,
                RatingCount = 2
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(unblockedFeedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(existingFeedbacks);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify product rating was recalculated: (5 + 3 + 4) / 3 = 4.0
            _mockProductRepository.Verify(x => x.GetByIdAsync(productId), Times.Once);
            _mockProductRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Product>(p =>
                p.Id == productId &&
                p.AverageRating == 4.0m &&
                p.RatingCount == 3
            )), Times.Once);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
        }

        /// <summary>
        /// Test: Unblock feedback clears violation reason
        /// Expected: ViolationReason is set to null and IsVisible is set to true
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_ShouldClearViolationReason()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var feedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 3,
                Comment = "Test",
                IsBlocked = true,
                IsVisible = false,
                ViolationReason = "Contains inappropriate language"
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(feedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { feedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify ViolationReason is cleared and IsVisible is set to true
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.Id == feedbackId &&
                f.IsVisible == true &&
                f.ViolationReason == null
            )), Times.Once);
        }

        /// <summary>
        /// Test: Unblock feedback without product (Order feedback)
        /// Expected: Unblocks successfully without recalculating product rating
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_OrderFeedback_ShouldUnblockWithoutRecalculation()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();

            var orderFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = null, // Order feedback, no product
                OrderId = Guid.NewGuid(),
                Rating = 3,
                Comment = "Order feedback",
                IsBlocked = true,
                IsVisible = false,
                ViolationReason = "Test violation"
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(orderFeedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.GetByIdAsync(feedbackId), Times.Once);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsVisible == true &&
                f.ViolationReason == null
            )), Times.Once);

            // Should not try to recalculate product rating
            _mockProductRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockProductRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()), Times.Never);
        }

        /// <summary>
        /// Test: Repository throws exception during unblock
        /// Expected: Returns error response with exception message
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_RepositoryThrowsException_ShouldReturnErrorResponse()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var exceptionMessage = "Database error";

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal($"Error: {exceptionMessage}", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
        }

        /// <summary>
        /// Test: Unblock already unblocked feedback
        /// Expected: Still returns success (idempotent operation)
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_AlreadyUnblocked_ShouldReturnSuccess()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var alreadyUnblockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "Already unblocked",
                IsBlocked = false,
                IsVisible = true,
                ViolationReason = null
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(alreadyUnblockedFeedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { alreadyUnblockedFeedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId, AverageRating = 5m, RatingCount = 1 });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
        }

        /// <summary>
        /// Test: Unblock feedback with empty GUID
        /// Expected: Returns "Feedback not found" message
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_EmptyGuid_ShouldReturnNotFound()
        {
            // Arrange
            var feedbackId = Guid.Empty;

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(false);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback not found", result.Message);
            Assert.False(result.Data);

            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);
        }

        /// <summary>
        /// Test: Unblock feedback sets correct properties in repository
        /// Expected: IsBlocked=false, IsVisible=true, BlockedAt and BlockedById are cleared
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_ShouldSetCorrectProperties()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var feedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 3,
                Comment = "Test",
                IsBlocked = true,
                IsVisible = false,
                BlockedAt = DateTime.UtcNow.AddDays(-1),
                BlockedById = Guid.NewGuid(),
                ViolationReason = "Test violation"
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(feedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { feedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify repository method was called with correct parameters
            _mockFeedbackRepository.Verify(x => x.UnblockFeedbackAsync(feedbackId), Times.Once);

            // Verify service updates IsVisible and ViolationReason
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.Id == feedbackId &&
                f.IsVisible == true &&
                f.ViolationReason == null
            )), Times.Once);
        }

        /// <summary>
        /// Test: Unblock first feedback should update product rating from 0
        /// Expected: Product rating is calculated when first feedback is unblocked
        /// </summary>
        [Fact]
        public async Task UnblockFeedback_FirstFeedback_ShouldCalculateRating()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var firstFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                ProductId = productId,
                Rating = 5,
                Comment = "First feedback",
                IsBlocked = false,
                IsVisible = true
            };

            var product = new BusinessObject.Models.Product {
                Id = productId,
                AverageRating = 0.0m,
                RatingCount = 0
            };

            _mockFeedbackRepository.Setup(x => x.UnblockFeedbackAsync(feedbackId))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(firstFeedback);

            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .ReturnsAsync(true);

            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { firstFeedback });

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            // Act
            var result = await _feedbackService.UnblockFeedbackAsync(feedbackId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Feedback unblocked successfully", result.Message);
            Assert.True(result.Data);

            // Verify product rating was calculated
            _mockProductRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Product>(p =>
                p.Id == productId &&
                p.AverageRating == 5.0m &&
                p.RatingCount == 1
            )), Times.Once);
        }

        #endregion

        #region Content Moderation Tests (Check Feedback for Violations Automatically)

        /// <summary>
        /// Test: Submit feedback with clean content
        /// Expected: Feedback is not blocked, IsVisible=true, no ViolationReason
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_CleanContent_ShouldNotBeBlocked()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 5,
                Comment = "Great product! Very satisfied."
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId };

            // Mock content moderation - clean content
            _mockContentModerationService.Setup(x => x.CheckContentAsync(dto.Comment))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null,
                    ViolatedTerms = new List<string>()
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new BusinessObject.Models.Feedback
                {
                    Id = id,
                    CustomerId = customerId,
                    ProductId = productId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    IsBlocked = false,
                    IsVisible = true,
                    ViolationReason = null
                });

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Rating);
            Assert.Equal("Great product! Very satisfied.", result.Comment);

            // Verify feedback was added with correct moderation status
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == false &&
                f.IsVisible == true &&
                f.ViolationReason == null
            )), Times.Once);

            _mockContentModerationService.Verify(x => x.CheckContentAsync(dto.Comment), Times.Once);
        }

        /// <summary>
        /// Test: Submit feedback with inappropriate content
        /// Expected: Feedback is automatically blocked, IsVisible=false, ViolationReason is set
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_InappropriateContent_ShouldBeBlockedAutomatically()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 1,
                Comment = "This product is fucking terrible"
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId };

            // Mock content moderation - inappropriate content detected
            _mockContentModerationService.Setup(x => x.CheckContentAsync(dto.Comment))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Contains profanity",
                    ViolatedTerms = new List<string> { "profanity", "fucking" }
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new BusinessObject.Models.Feedback
                {
                    Id = id,
                    CustomerId = customerId,
                    ProductId = productId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    IsBlocked = true,
                    IsVisible = false,
                    ViolationReason = "Contains profanity"
                });

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);

            // Verify feedback was added with blocked status
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == true &&
                f.IsVisible == false &&
                f.ViolationReason == "Contains profanity" &&
                f.BlockedById == null // Auto-blocked by AI, not by staff
            )), Times.Once);

            _mockContentModerationService.Verify(x => x.CheckContentAsync(dto.Comment), Times.Once);
        }

        /// <summary>
        /// Test: Submit feedback with null/empty comment
        /// Expected: Content moderation is called with empty string, feedback is not blocked
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_EmptyComment_ShouldCheckModeration()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 4,
                Comment = null // Empty comment
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId };

            // Mock content moderation - empty content is appropriate
            _mockContentModerationService.Setup(x => x.CheckContentAsync(""))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null,
                    ViolatedTerms = new List<string>()
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new BusinessObject.Models.Feedback
                {
                    Id = id,
                    CustomerId = customerId,
                    ProductId = productId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    IsBlocked = false,
                    IsVisible = true
                });

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);

            // Verify content moderation was called with empty string
            _mockContentModerationService.Verify(x => x.CheckContentAsync(""), Times.Once);

            // Verify feedback was added without blocking
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == false &&
                f.IsVisible == true
            )), Times.Once);
        }

        /// <summary>
        /// Test: Submit feedback with hate speech
        /// Expected: Feedback is blocked with appropriate violation reason
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_HateSpeech_ShouldBeBlockedWithReason()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 1,
                Comment = "Only for white people, no blacks allowed"
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId };

            // Mock content moderation - hate speech detected
            _mockContentModerationService.Setup(x => x.CheckContentAsync(dto.Comment))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Contains hate speech and racial discrimination",
                    ViolatedTerms = new List<string> { "hate_speech", "discrimination", "racial" }
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new BusinessObject.Models.Feedback
                {
                    Id = id,
                    CustomerId = customerId,
                    ProductId = productId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    IsBlocked = true,
                    IsVisible = false,
                    ViolationReason = "Contains hate speech and racial discrimination"
                });

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);

            // Verify feedback was blocked with correct reason
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == true &&
                f.IsVisible == false &&
                f.ViolationReason == "Contains hate speech and racial discrimination"
            )), Times.Once);

            _mockContentModerationService.Verify(x => x.CheckContentAsync(dto.Comment), Times.Once);
        }

        /// <summary>
        /// Test: Submit feedback with spam content
        /// Expected: Feedback is blocked as spam
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_SpamContent_ShouldBeBlocked()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 1,
                Comment = "aaaaaaaaaaaaaaaa"
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId };

            // Mock content moderation - spam detected
            _mockContentModerationService.Setup(x => x.CheckContentAsync(dto.Comment))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Contains excessive repeating characters",
                    ViolatedTerms = new List<string> { "spam", "repeating_characters" }
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new BusinessObject.Models.Feedback
                {
                    Id = id,
                    CustomerId = customerId,
                    ProductId = productId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    IsBlocked = true,
                    IsVisible = false,
                    ViolationReason = "Contains excessive repeating characters"
                });

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = Guid.NewGuid(),
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);

            // Verify feedback was blocked as spam
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == true &&
                f.IsVisible == false &&
                f.ViolationReason == "Contains excessive repeating characters"
            )), Times.Once);

            _mockContentModerationService.Verify(x => x.CheckContentAsync(dto.Comment), Times.Once);
        }

        /// <summary>
        /// Test: Blocked feedback should not affect product rating
        /// Expected: Product rating is not recalculated when feedback is auto-blocked
        /// Note: This is tested indirectly - blocked feedbacks are excluded from rating calculation
        /// </summary>
        [Fact]
        public async Task SubmitFeedback_BlockedFeedback_ShouldNotAffectProductRating()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var orderItemId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var dto = new FeedbackRequestDto
            {
                TargetType = FeedbackTargetType.Product,
                TargetId = productId,
                OrderItemId = orderItemId,
                Rating = 1,
                Comment = "Inappropriate content here"
            };

            var orderItem = new OrderItem
            {
                Id = orderItemId,
                ProductId = productId,
                OrderId = orderId
            };

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.in_use
            };

            var product = new BusinessObject.Models.Product { Id = productId, AverageRating = 5.0m, RatingCount = 1 };

            // Mock content moderation - inappropriate
            _mockContentModerationService.Setup(x => x.CheckContentAsync(dto.Comment))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Inappropriate content",
                    ViolatedTerms = new List<string> { "inappropriate" }
                });

            _mockOrderItemRepository.Setup(x => x.GetByIdAsync(orderItemId))
                .ReturnsAsync(orderItem);

            _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(product);

            _mockFeedbackRepository.Setup(x => x.HasUserFeedbackedOrderItemAsync(customerId, orderItemId))
                .ReturnsAsync(false);

            _mockFeedbackRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.Feedback>())).Returns(Task.CompletedTask);

            var blockedFeedback = new BusinessObject.Models.Feedback
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductId = productId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                IsBlocked = true,
                IsVisible = false,
                ViolationReason = "Inappropriate content"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(blockedFeedback);

            // Only return non-blocked feedbacks for rating calculation
            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback>
                {
                    new BusinessObject.Models.Feedback { Rating = 5, IsBlocked = false }
                    // Blocked feedback is not included
                });

            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.Feedback>(dto))
                .Returns(new BusinessObject.Models.Feedback());

            _mockMapper.Setup(x => x.Map<FeedbackResponseDto>(It.IsAny<BusinessObject.Models.Feedback>()))
                .Returns(new FeedbackResponseDto
                {
                    FeedbackId = blockedFeedback.Id,
                    Rating = dto.Rating,
                    Comment = dto.Comment
                });

            // Act
            var result = await _feedbackService.SubmitFeedbackAsync(dto, customerId);

            // Assert
            Assert.NotNull(result);

            // Verify feedback was blocked
            _mockFeedbackRepository.Verify(x => x.AddAsync(It.Is<BusinessObject.Models.Feedback>(f =>
                f.IsBlocked == true
            )), Times.Once);

            // Verify product rating calculation only includes non-blocked feedbacks
            _mockProductRepository.Verify(x => x.UpdateAsync(It.Is<BusinessObject.Models.Product>(p =>
                p.AverageRating == 5.0m && // Only the non-blocked feedback
                p.RatingCount == 1
            )), Times.Once);
        }

        #endregion

        #region UpdateCustomerFeedbackAsync Tests

        /// <summary>
        /// Test: Update feedback with clean content - should succeed
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_WithCleanContent_ShouldUpdateSuccessfully()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId,
                ProductId = productId,
                TargetType = FeedbackTargetType.Product,
                Rating = 3,
                Comment = "Old comment",
                IsBlocked = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 5,
                Comment = "Updated clean comment"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(customerId))
                .ReturnsAsync(new User { Id = customerId, Role = UserRole.customer });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("Updated clean comment", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });
            BusinessObject.Models.Feedback capturedFeedback = null;
            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .Callback<BusinessObject.Models.Feedback>(f => capturedFeedback = f)
                .ReturnsAsync(true);

            // Act
            await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId);

            // Assert
            Assert.NotNull(capturedFeedback);
            Assert.Equal(5, capturedFeedback.Rating);
            Assert.Equal("Updated clean comment", capturedFeedback.Comment);
            Assert.False(capturedFeedback.IsBlocked);
            Assert.True(capturedFeedback.IsVisible);
            Assert.NotNull(capturedFeedback.UpdatedAt);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(capturedFeedback), Times.Once);
        }

        /// <summary>
        /// Test: Update feedback with violating content - should throw exception and block feedback
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_WithViolatingContent_ShouldThrowExceptionAndBlock()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId,
                Rating = 4,
                Comment = "Old clean comment",
                IsBlocked = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 4,
                Comment = "This contains offensive content"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(customerId))
                .ReturnsAsync(new User { Id = customerId, Role = UserRole.customer });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("This contains offensive content", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = false,
                    Reason = "Contains offensive language"
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId));

            Assert.Contains("violates community guidelines", exception.Message);

            // Verify feedback was NOT updated (because exception was thrown)
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()), Times.Never);
        }

        /// <summary>
        /// Test: Update blocked feedback with clean content - should unblock and make visible
        /// This is the key test for the bug fix
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_BlockedFeedbackWithCleanContent_ShouldUnblockAndMakeVisible()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId,
                ProductId = productId,
                TargetType = FeedbackTargetType.Product,
                Rating = 3,
                Comment = "Previously blocked comment",
                IsBlocked = true,
                IsFlagged = true,
                IsVisible = false,
                ViolationReason = "Previous violation",
                BlockedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 5,
                Comment = "Now this is a clean comment"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(customerId))
                .ReturnsAsync(new User { Id = customerId, Role = UserRole.customer });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("Now this is a clean comment", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });
            BusinessObject.Models.Feedback capturedFeedback = null;
            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .Callback<BusinessObject.Models.Feedback>(f => capturedFeedback = f)
                .ReturnsAsync(true);

            // Act
            await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId);

            // Assert - Verify feedback was unblocked and made visible
            Assert.NotNull(capturedFeedback);
            Assert.False(capturedFeedback.IsBlocked);
            Assert.False(capturedFeedback.IsFlagged);
            Assert.True(capturedFeedback.IsVisible);
            Assert.Null(capturedFeedback.ViolationReason);
            Assert.Null(capturedFeedback.BlockedAt);
            Assert.Null(capturedFeedback.BlockedById);
            Assert.Equal(5, capturedFeedback.Rating);
            Assert.Equal("Now this is a clean comment", capturedFeedback.Comment);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(capturedFeedback), Times.Once);
        }

        /// <summary>
        /// Test: Update invisible feedback with clevan content - should make visible
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_InvisibleFeedbackWithCleanContent_ShouldMakeVisible()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId,
                Rating = 3,
                Comment = "Hidden comment",
                IsBlocked = false,
                IsVisible = false, // Invisible but not blocked
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 4,
                Comment = "Updated clean comment"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(customerId))
                .ReturnsAsync(new User { Id = customerId, Role = UserRole.customer });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("Updated clean comment", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });
            BusinessObject.Models.Feedback capturedFeedback = null;
            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .Callback<BusinessObject.Models.Feedback>(f => capturedFeedback = f)
                .ReturnsAsync(true);

            // Act
            await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId);

            // Assert
            Assert.NotNull(capturedFeedback);
            Assert.True(capturedFeedback.IsVisible);
            Assert.False(capturedFeedback.IsBlocked);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(capturedFeedback), Times.Once);
        }

        /// <summary>
        /// Test: Update feedback - unauthorized user should throw exception
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_UnauthorizedUser_ShouldThrowException()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var differentCustomerId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId, // Different from the one trying to update
                Rating = 4,
                Comment = "Original comment"
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 5,
                Comment = "Trying to update"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(differentCustomerId))
                .ReturnsAsync(new User { Id = differentCustomerId, Role = UserRole.customer });

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, differentCustomerId));

            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()), Times.Never);
        }

        /// <summary>
        /// Test: Update feedback - non-existent feedback should throw exception
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_NonExistentFeedback_ShouldThrowException()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 5,
                Comment = "Update attempt"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync((BusinessObject.Models.Feedback)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId));

            _mockFeedbackRepository.Verify(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()), Times.Never);
        }

        /// <summary>
        /// Test: Update feedback rating only (no comment change) - should still check existing comment
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_RatingOnlyUpdate_ShouldCheckExistingComment()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId,
                ProductId = productId,
                TargetType = FeedbackTargetType.Product,
                Rating = 3,
                Comment = "Existing clean comment",
                IsBlocked = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 5,
                Comment = null // Not updating comment
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(customerId))
                .ReturnsAsync(new User { Id = customerId, Role = UserRole.customer });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("Existing clean comment", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });
            _mockProductRepository.Setup(x => x.GetByIdAsync(productId))
                .ReturnsAsync(new BusinessObject.Models.Product { Id = productId, AverageRating = 4.0m, RatingCount = 2 });
            _mockFeedbackRepository.Setup(x => x.GetFeedbacksByTargetAsync(FeedbackTargetType.Product, productId))
                .ReturnsAsync(new List<BusinessObject.Models.Feedback> { existingFeedback });
            _mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Product>()))
                .ReturnsAsync(true);

            BusinessObject.Models.Feedback capturedFeedback = null;
            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .Callback<BusinessObject.Models.Feedback>(f => capturedFeedback = f)
                .ReturnsAsync(true);

            // Act
            await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, customerId);

            // Assert
            _mockContentModerationService.Verify(x => x.CheckFeedbackContentAsync("Existing clean comment", null), Times.Once);
            Assert.NotNull(capturedFeedback);
            Assert.Equal(5, capturedFeedback.Rating);
            Assert.Null(capturedFeedback.Comment);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(capturedFeedback), Times.Once);
        }

        /// <summary>
        /// Test: Admin can update any feedback
        /// </summary>
        [Fact]
        public async Task UpdateCustomerFeedback_AdminUser_ShouldAllowUpdate()
        {
            // Arrange
            var feedbackId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var existingFeedback = new BusinessObject.Models.Feedback
            {
                Id = feedbackId,
                CustomerId = customerId, // Different from admin
                Rating = 3,
                Comment = "Customer comment",
                IsBlocked = false,
                IsVisible = true
            };

            var updateDto = new UpdateFeedbackDto
            {
                Rating = 4,
                Comment = "Admin updated comment"
            };

            _mockFeedbackRepository.Setup(x => x.GetByIdAsync(feedbackId))
                .ReturnsAsync(existingFeedback);
            _mockUserRepository.Setup(x => x.GetByIdAsync(adminId))
                .ReturnsAsync(new User { Id = adminId, Role = UserRole.admin });
            _mockContentModerationService.Setup(x => x.CheckFeedbackContentAsync("Admin updated comment", null))
                .ReturnsAsync(new BusinessObject.DTOs.ProductDto.ContentModerationResultDTO
                {
                    IsAppropriate = true,
                    Reason = null
                });
            BusinessObject.Models.Feedback capturedFeedback = null;
            _mockFeedbackRepository.Setup(x => x.UpdateAsync(It.IsAny<BusinessObject.Models.Feedback>()))
                .Callback<BusinessObject.Models.Feedback>(f => capturedFeedback = f)
                .ReturnsAsync(true);

            // Act
            await _feedbackService.UpdateCustomerFeedbackAsync(feedbackId, updateDto, adminId);

            // Assert
            Assert.NotNull(capturedFeedback);
            Assert.Equal(4, capturedFeedback.Rating);
            Assert.Equal("Admin updated comment", capturedFeedback.Comment);
            _mockFeedbackRepository.Verify(x => x.UpdateAsync(capturedFeedback), Times.Once);
        }

        #endregion
    }
}