using AutoMapper;
using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.FeedbackRepositories;
using Repositories.OrderRepositories;
using Repositories.RepositoryBase;
using Services.FeedbackServices;

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
    }
}

