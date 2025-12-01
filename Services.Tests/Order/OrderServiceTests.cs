using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using Moq;
using Repositories.OrderRepositories;
using Services.OrderServices;
using Xunit;
using Profile = BusinessObject.Models.Profile;

namespace Services.Tests.OrderTests
{
    /// <summary>
    /// Unit tests for Order Service - View All Orders functionality (Staff/Admin)
    /// Tests the GetAllOrdersForAdminAsync method
    /// 
    /// Test Coverage:
    /// - Get all orders successfully
    /// - Handle empty order list
    /// - Handle orders with different transaction types (rental/purchase)
    /// - Handle orders with different statuses
    /// - Handle orders with missing customer/provider information
    /// - Handle repository exceptions
    /// 
    /// Messages verified:
    /// - No specific API messages for this read-only operation
    /// - Data integrity and mapping are verified
    /// </summary>
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly OrderService _orderService;

        public OrderServiceTests()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockMapper = new Mock<IMapper>();
            
            // Create OrderService with minimal dependencies for GetAllOrdersForAdminAsync
            // Note: This method only uses _orderRepo, so other dependencies can be null
            _orderService = new OrderService(
                _mockOrderRepository.Object,
                null, // notificationService
                null, // hubContext
                null, // productRepository
                null, // orderItemRepository
                _mockMapper.Object,
                null, // cartRepository
                null, // userRepository
                null, // context
                null, // emailRepository
                null, // productRepo
                null, // notificationRepository
                null, // systemConfigRepository
                null  // discountCalculationService
            );
        }

        /// <summary>
        /// Test: Get all orders successfully with rental and purchase orders
        /// Expected: Returns list of orders with correct transaction types
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_WithMixedOrders_ShouldReturnAllOrders()
        {
            // Arrange
            var customerId = System.Guid.NewGuid();
            var providerId = System.Guid.NewGuid();
            
            var orders = new List<BusinessObject.Models.Order>
            {
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = customerId,
                    ProviderId = providerId,
                    Status = OrderStatus.pending,
                    Subtotal = 1000m,
                    DiscountAmount = 100m,
                    TotalAmount = 900m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = new User
                    {
                        Email = "customer@test.com",
                        Profile = new Profile { FullName = "John Doe" }
                    },
                    Provider = new User
                    {
                        Email = "provider@test.com",
                        Profile = new Profile { FullName = "Provider Shop" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            TransactionType = TransactionType.rental,
                            Quantity = 1,
                            DailyRate = 100m,
                            RentalDays = 10
                        }
                    },
                    RentalStart = System.DateTime.UtcNow,
                    RentalEnd = System.DateTime.UtcNow.AddDays(10)
                },
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = customerId,
                    ProviderId = providerId,
                    Status = OrderStatus.approved,
                    Subtotal = 500m,
                    DiscountAmount = 0m,
                    TotalAmount = 500m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = new User
                    {
                        Email = "customer2@test.com",
                        Profile = new Profile { FullName = "Jane Smith" }
                    },
                    Provider = new User
                    {
                        Email = "provider2@test.com",
                        Profile = new Profile { FullName = "Provider Store" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            TransactionType = TransactionType.purchase,
                            Quantity = 2,
                            DailyRate = 250m
                        }
                    }
                }
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            
            // Verify rental order
            var rentalOrder = resultList.FirstOrDefault(o => o.TransactionType == "rental");
            Assert.NotNull(rentalOrder);
            Assert.Equal("John Doe", rentalOrder.CustomerName);
            Assert.Equal("customer@test.com", rentalOrder.CustomerEmail);
            Assert.Equal("Provider Shop", rentalOrder.ProviderName);
            Assert.Equal(OrderStatus.pending, rentalOrder.Status);
            Assert.Equal(900m, rentalOrder.TotalAmount); // Subtotal - Discount
            Assert.NotNull(rentalOrder.RentalStartDate);
            Assert.NotNull(rentalOrder.RentalEndDate);
            
            // Verify purchase order
            var purchaseOrder = resultList.FirstOrDefault(o => o.TransactionType == "purchase");
            Assert.NotNull(purchaseOrder);
            Assert.Equal("Jane Smith", purchaseOrder.CustomerName);
            Assert.Equal(OrderStatus.approved, purchaseOrder.Status);
            Assert.Equal(500m, purchaseOrder.TotalAmount);
            Assert.Null(purchaseOrder.RentalStartDate);
            Assert.Null(purchaseOrder.RentalEndDate);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders when no orders exist
        /// Expected: Returns empty list
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_NoOrders_ShouldReturnEmptyList()
        {
            // Arrange
            var emptyOrders = new List<BusinessObject.Models.Order>();

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(emptyOrders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders with different statuses
        /// Expected: Returns all orders regardless of status
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_WithDifferentStatuses_ShouldReturnAllStatuses()
        {
            // Arrange
            var customerId = System.Guid.NewGuid();
            var providerId = System.Guid.NewGuid();
            
            var orders = new List<BusinessObject.Models.Order>
            {
                CreateTestOrder(customerId, providerId, OrderStatus.pending),
                CreateTestOrder(customerId, providerId, OrderStatus.approved),
                CreateTestOrder(customerId, providerId, OrderStatus.in_transit),
                CreateTestOrder(customerId, providerId, OrderStatus.in_use),
                CreateTestOrder(customerId, providerId, OrderStatus.returned),
                CreateTestOrder(customerId, providerId, OrderStatus.cancelled)
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(6, resultList.Count);
            
            // Verify all statuses are present
            Assert.Contains(resultList, o => o.Status == OrderStatus.pending);
            Assert.Contains(resultList, o => o.Status == OrderStatus.approved);
            Assert.Contains(resultList, o => o.Status == OrderStatus.in_transit);
            Assert.Contains(resultList, o => o.Status == OrderStatus.in_use);
            Assert.Contains(resultList, o => o.Status == OrderStatus.returned);
            Assert.Contains(resultList, o => o.Status == OrderStatus.cancelled);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders with missing customer information
        /// Expected: Returns orders with "N/A" for missing customer data
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_WithMissingCustomerInfo_ShouldReturnNA()
        {
            // Arrange
            var orders = new List<BusinessObject.Models.Order>
            {
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = System.Guid.NewGuid(),
                    ProviderId = System.Guid.NewGuid(),
                    Status = OrderStatus.pending,
                    Subtotal = 1000m,
                    DiscountAmount = 0m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = null, // Missing customer
                    Provider = new User
                    {
                        Email = "provider@test.com",
                        Profile = new Profile { FullName = "Provider Shop" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem { TransactionType = TransactionType.rental }
                    }
                }
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal("N/A", resultList[0].CustomerName);
            Assert.Equal("N/A", resultList[0].CustomerEmail);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders with missing provider information
        /// Expected: Returns orders with "N/A" for missing provider data
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_WithMissingProviderInfo_ShouldReturnNA()
        {
            // Arrange
            var orders = new List<BusinessObject.Models.Order>
            {
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = System.Guid.NewGuid(),
                    ProviderId = System.Guid.NewGuid(),
                    Status = OrderStatus.pending,
                    Subtotal = 1000m,
                    DiscountAmount = 0m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = new User
                    {
                        Email = "customer@test.com",
                        Profile = new Profile { FullName = "John Doe" }
                    },
                    Provider = null, // Missing provider
                    Items = new List<OrderItem>
                    {
                        new OrderItem { TransactionType = TransactionType.purchase }
                    }
                }
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal("N/A", resultList[0].ProviderName);
            Assert.Equal("N/A", resultList[0].ProviderEmail);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders calculates display total correctly
        /// Expected: TotalAmount = Subtotal - DiscountAmount
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_CalculatesDisplayTotal_Correctly()
        {
            // Arrange
            var orders = new List<BusinessObject.Models.Order>
            {
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = System.Guid.NewGuid(),
                    ProviderId = System.Guid.NewGuid(),
                    Status = OrderStatus.pending,
                    Subtotal = 1000m,
                    DiscountAmount = 150m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = new User
                    {
                        Email = "customer@test.com",
                        Profile = new Profile { FullName = "John Doe" }
                    },
                    Provider = new User
                    {
                        Email = "provider@test.com",
                        Profile = new Profile { FullName = "Provider Shop" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem { TransactionType = TransactionType.rental }
                    }
                }
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal(1000m, resultList[0].Subtotal);
            Assert.Equal(150m, resultList[0].DiscountAmount);
            Assert.Equal(850m, resultList[0].TotalAmount); // 1000 - 150

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Repository throws exception
        /// Expected: Exception is propagated to caller
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_RepositoryThrowsException_ShouldPropagateException()
        {
            // Arrange
            var exceptionMessage = "Database connection failed";
            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ThrowsAsync(new System.Exception(exceptionMessage));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<System.Exception>(
                () => _orderService.GetAllOrdersForAdminAsync()
            );
            
            Assert.Equal(exceptionMessage, exception.Message);
            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        /// <summary>
        /// Test: Get all orders with zero discount
        /// Expected: TotalAmount equals Subtotal
        /// </summary>
        [Fact]
        public async Task GetAllOrdersForAdmin_WithZeroDiscount_ShouldReturnSubtotalAsTotal()
        {
            // Arrange
            var orders = new List<BusinessObject.Models.Order>
            {
                new BusinessObject.Models.Order
                {
                    Id = System.Guid.NewGuid(),
                    CustomerId = System.Guid.NewGuid(),
                    ProviderId = System.Guid.NewGuid(),
                    Status = OrderStatus.approved,
                    Subtotal = 500m,
                    DiscountAmount = 0m,
                    CreatedAt = System.DateTime.UtcNow,
                    Customer = new User
                    {
                        Email = "customer@test.com",
                        Profile = new Profile { FullName = "John Doe" }
                    },
                    Provider = new User
                    {
                        Email = "provider@test.com",
                        Profile = new Profile { FullName = "Provider Shop" }
                    },
                    Items = new List<OrderItem>
                    {
                        new OrderItem { TransactionType = TransactionType.purchase }
                    }
                }
            };

            _mockOrderRepository.Setup(x => x.GetAllOrdersBasicAsync())
                .ReturnsAsync(orders);

            // Act
            var result = await _orderService.GetAllOrdersForAdminAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Single(resultList);
            Assert.Equal(500m, resultList[0].Subtotal);
            Assert.Equal(0m, resultList[0].DiscountAmount);
            Assert.Equal(500m, resultList[0].TotalAmount);

            _mockOrderRepository.Verify(x => x.GetAllOrdersBasicAsync(), Times.Once);
        }

        // Helper method to create test orders
        private BusinessObject.Models.Order CreateTestOrder(System.Guid customerId, System.Guid providerId, OrderStatus status)
        {
            return new BusinessObject.Models.Order
            {
                Id = System.Guid.NewGuid(),
                CustomerId = customerId,
                ProviderId = providerId,
                Status = status,
                Subtotal = 1000m,
                DiscountAmount = 100m,
                CreatedAt = System.DateTime.UtcNow,
                Customer = new User
                {
                    Email = "customer@test.com",
                    Profile = new Profile { FullName = "Test Customer" }
                },
                Provider = new User
                {
                    Email = "provider@test.com",
                    Profile = new Profile { FullName = "Test Provider" }
                },
                Items = new List<OrderItem>
                {
                    new OrderItem { TransactionType = TransactionType.rental }
                }
            };
        }
    }
}


