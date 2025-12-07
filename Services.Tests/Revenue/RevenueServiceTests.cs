using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.BankAccountRepositories;
using Repositories.RevenueRepositories;
using Repositories.TransactionRepositories;
using Repositories.WithdrawalRepositories;
using Services.RevenueServices;
using System.Collections.Generic;

namespace Services.Tests.Revenue
{
    public class RevenueServiceTests
    {
        private readonly Mock<IRevenueRepository> _revenueRepo;
        private readonly Mock<ITransactionRepository> _transactionRepo;
        private readonly Mock<IBankAccountRepository> _bankAccountRepo;
        private readonly Mock<IWithdrawalRepository> _withdrawalRepo;
        private readonly Mock<IMapper> _mapper;
        private readonly RevenueService _service;

        public RevenueServiceTests()
        {
            _revenueRepo = new Mock<IRevenueRepository>();
            _transactionRepo = new Mock<ITransactionRepository>();
            _bankAccountRepo = new Mock<IBankAccountRepository>();
            _withdrawalRepo = new Mock<IWithdrawalRepository>();
            _mapper = new Mock<IMapper>();

            _service = new RevenueService(
                _revenueRepo.Object,
                _transactionRepo.Object,
                _bankAccountRepo.Object,
                _withdrawalRepo.Object,
                _mapper.Object);
        }

        #region GetRevenueStatsAsync

        [Fact]
        public async Task UTCID01_GetRevenueStatsAsync_ShouldReturnProviderMetrics()
        {
            var providerId = Guid.NewGuid();
            var start = new DateTime(2024, 1, 1);
            var end = start.AddMonths(1);
            var prevStart = start.AddMonths(-1);
            var prevEnd = start;

            var returnedOrder = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = providerId,
                Status = OrderStatus.returned,
                Subtotal = 200,
                CreatedAt = start.AddDays(1),
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        DailyRate = 50,
                        RentalDays = 2,
                        Quantity = 2,
                        TransactionType = TransactionType.rental,
                        CommissionAmount = 20
                    }
                }
            };

            var previousReturned = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = providerId,
                Status = OrderStatus.returned,
                Subtotal = 100,
                CreatedAt = prevStart.AddDays(1),
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        DailyRate = 100,
                        RentalDays = 1,
                        Quantity = 1,
                        TransactionType = TransactionType.purchase,
                        CommissionAmount = 10
                    }
                }
            };

            var currentReturnedOrders = new List<Order> { returnedOrder };
            var previousReturnedOrders = new List<Order> { previousReturned };
            var currentAllOrders = new List<Order>
            {
                returnedOrder,
                new Order { Id = Guid.NewGuid(), Status = OrderStatus.pending, CreatedAt = start.AddDays(2), Subtotal = 50 }
            };
            var previousAllOrders = new List<Order> { previousReturned };

            _revenueRepo.Setup(r => r.GetOrdersInPeriodAsync(providerId, start, end))
                .ReturnsAsync(currentReturnedOrders);
            _revenueRepo.Setup(r => r.GetOrdersInPeriodAsync(providerId, prevStart, prevEnd))
                .ReturnsAsync(previousReturnedOrders);
            _revenueRepo.Setup(r => r.GetAllOrdersInPeriodAsync(providerId, start, end))
                .ReturnsAsync(currentAllOrders);
            _revenueRepo.Setup(r => r.GetAllOrdersInPeriodAsync(providerId, prevStart, prevEnd))
                .ReturnsAsync(previousAllOrders);
            _revenueRepo.Setup(r => r.GetPenaltyRevenueInPeriodAsync(providerId, start, end))
                .ReturnsAsync(30);
            _revenueRepo.Setup(r => r.GetPenaltyRevenueInPeriodAsync(providerId, prevStart, prevEnd))
                .ReturnsAsync(0);

            var result = await _service.GetRevenueStatsAsync(providerId, "month", start, end);

            Assert.Equal(200, result.CurrentPeriodRevenue);
            Assert.Equal(100, result.PreviousPeriodRevenue);
            Assert.Equal(1, result.CurrentPeriodOrders);
            Assert.Equal(1, result.PreviousPeriodOrders);
            Assert.Equal(30, result.NetRevenueFromPenalties);
            Assert.True(result.ChartData.Count > 0);
            Assert.Contains(result.StatusBreakdown, b => b.Status == OrderStatus.returned.ToString());
        }

        #endregion

        #region GetPayoutSummaryAsync

        [Fact]
        public async Task UTCID02_GetPayoutSummaryAsync_ShouldReturnBalanceBreakdown()
        {
            var providerId = Guid.NewGuid();
            _revenueRepo.Setup(r => r.GetTotalEarningsAsync(providerId)).ReturnsAsync(1000);
            _withdrawalRepo.Setup(r => r.GetTotalCompletedAmountAsync(providerId)).ReturnsAsync(400);
            _withdrawalRepo.Setup(r => r.GetTotalPendingAmountAsync(providerId)).ReturnsAsync(150);
            var transactions = new List<Transaction>
            {
                new Transaction { Id = Guid.NewGuid(), Amount = 100, TransactionDate = DateTime.UtcNow }
            };
            _transactionRepo.Setup(t => t.GetRecentPayoutsAsync(providerId, 5))
                .ReturnsAsync(transactions);

            var summary = await _service.GetPayoutSummaryAsync(providerId);

            Assert.Equal(600, summary.CurrentBalance);
            Assert.Equal(150, summary.PendingAmount);
            Assert.Single(summary.RecentPayouts);
            _transactionRepo.Verify(t => t.GetRecentPayoutsAsync(providerId, 5), Times.Once);
        }

        #endregion

        #region GetPayoutHistoryAsync

        [Fact]
        public async Task UTCID03_GetPayoutHistoryAsync_ShouldMapTransactionsToHistory()
        {
            var providerId = Guid.NewGuid();
            var transactions = new List<Transaction>
            {
                new Transaction { Id = Guid.NewGuid(), Amount = 250, TransactionDate = DateTime.UtcNow }
            };
            _transactionRepo.Setup(t => t.GetPayoutHistoryAsync(providerId, 2, 5))
                .ReturnsAsync(transactions);

            var history = await _service.GetPayoutHistoryAsync(providerId, 2, 5);

            Assert.Single(history);
            Assert.Equal(transactions[0].Id, history[0].Id);
            Assert.Equal(250, history[0].Amount);
        }

        #endregion

        #region RequestPayoutAsync

        [Fact]
        public async Task UTCID04_RequestPayoutAsync_ShouldPersistTransaction()
        {
            var providerId = Guid.NewGuid();
            Transaction? captured = null;
            _transactionRepo.Setup(t => t.AddTransactionAsync(It.IsAny<Transaction>()))
                .Callback<Transaction>(tx => captured = tx)
                .Returns(Task.CompletedTask);

            var result = await _service.RequestPayoutAsync(providerId, 300);

            Assert.True(result);
            Assert.NotNull(captured);
            Assert.Equal(providerId, captured!.CustomerId);
            Assert.Equal(300, captured.Amount);
            Assert.Equal(TransactionStatus.completed, captured.Status);
        }

        #endregion

        #region GetTopRevenueByProductAsync

        [Fact]
        public async Task UTCID05_GetTopRevenueByProductAsync_WithPeriod_ShouldReturnTopProducts()
        {
            // Arrange
            var providerId = Guid.NewGuid();

            var topProducts = new List<TopRevenueItemDto>
            {
                new TopRevenueItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Product A",
                    ProductImageUrl = "https://example.com/image.jpg",
                    Revenue = 5000,
                    OrderCount = 5,
                    TransactionType = "rental"
                },
                new TopRevenueItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Product B",
                    ProductImageUrl = "https://example.com/image2.jpg",
                    Revenue = 3000,
                    OrderCount = 3,
                    TransactionType = "purchase"
                }
            };

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5, It.IsAny<TransactionType?>()))
                .ReturnsAsync(topProducts);

            // Act
            var result = await _service.GetTopRevenueByProductAsync(providerId, "month", null, null, 5);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Product A", result[0].ProductName);
            Assert.Equal(5000, result[0].Revenue);
            Assert.Equal(5, result[0].OrderCount);
            Assert.Equal("rental", result[0].TransactionType);
            _revenueRepo.Verify(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5, It.IsAny<TransactionType?>()), Times.Once);
        }

        [Fact]
        public async Task UTCID06_GetTopRevenueByProductAsync_WithCustomDates_ShouldUseProvidedDates()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var startDate = new DateTime(2024, 2, 1);
            var endDate = new DateTime(2024, 2, 28);

            var topProducts = new List<TopRevenueItemDto>
            {
                new TopRevenueItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Product C",
                    Revenue = 2000,
                    OrderCount = 2,
                    TransactionType = "rental"
                }
            };

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, startDate, endDate, 5, It.IsAny<TransactionType?>()))
                .ReturnsAsync(topProducts);

            // Act
            var result = await _service.GetTopRevenueByProductAsync(providerId, "month", startDate, endDate, 5);

            // Assert
            Assert.Single(result);
            _revenueRepo.Verify(r => r.GetTopRevenueByProductAsync(providerId, startDate, endDate, 5, It.IsAny<TransactionType?>()), Times.Once);
        }

        [Fact]
        public async Task UTCID07_GetTopRevenueByProductAsync_WeekPeriod_ShouldCalculateWeekRange()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var topProducts = new List<TopRevenueItemDto>();

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5, It.IsAny<TransactionType?>()))
                .ReturnsAsync(topProducts);

            // Act
            await _service.GetTopRevenueByProductAsync(providerId, "week", null, null, 5);

            // Assert
            _revenueRepo.Verify(r => r.GetTopRevenueByProductAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                5,
                It.IsAny<TransactionType?>()), Times.Once);
        }

        [Fact]
        public async Task UTCID08_GetTopRevenueByProductAsync_YearPeriod_ShouldCalculateYearRange()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var topProducts = new List<TopRevenueItemDto>();

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5, It.IsAny<TransactionType?>()))
                .ReturnsAsync(topProducts);

            // Act
            await _service.GetTopRevenueByProductAsync(providerId, "year", null, null, 5);

            // Assert
            _revenueRepo.Verify(r => r.GetTopRevenueByProductAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                5,
                It.IsAny<TransactionType?>()), Times.Once);
        }

        [Fact]
        public async Task UTCID09_GetTopRevenueByProductAsync_EmptyResult_ShouldReturnEmptyList()
        {
            // Arrange
            var providerId = Guid.NewGuid();

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5, It.IsAny<TransactionType?>()))
                .ReturnsAsync(new List<TopRevenueItemDto>());

            // Act
            var result = await _service.GetTopRevenueByProductAsync(providerId, "month", null, null, 5);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task UTCID10_GetTopRevenueByProductAsync_CustomLimit_ShouldRespectLimit()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var limit = 10;

            var topProducts = new List<TopRevenueItemDto>();

            _revenueRepo.Setup(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), limit, It.IsAny<TransactionType?>()))
                .ReturnsAsync(topProducts);

            // Act
            await _service.GetTopRevenueByProductAsync(providerId, "month", null, null, limit);

            // Assert
            _revenueRepo.Verify(r => r.GetTopRevenueByProductAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), limit, It.IsAny<TransactionType?>()), Times.Once);
        }

        #endregion

        #region GetTopCustomersAsync

        [Fact]
        public async Task UTCID11_GetTopCustomersAsync_WithPeriod_ShouldReturnTopCustomers()
        {
            // Arrange
            var providerId = Guid.NewGuid();

            var topCustomers = new List<TopCustomerDto>
            {
                new TopCustomerDto
                {
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "John Doe",
                    CustomerEmail = "john@example.com",
                    CustomerAvatarUrl = "https://example.com/avatar.jpg",
                    TotalSpent = 10000,
                    OrderCount = 8
                },
                new TopCustomerDto
                {
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Jane Smith",
                    CustomerEmail = "jane@example.com",
                    TotalSpent = 7500,
                    OrderCount = 5
                }
            };

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _service.GetTopCustomersAsync(providerId, "month", null, null, 5);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("John Doe", result[0].CustomerName);
            Assert.Equal(10000, result[0].TotalSpent);
            Assert.Equal(8, result[0].OrderCount);
            _revenueRepo.Verify(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5), Times.Once);
        }

        [Fact]
        public async Task UTCID12_GetTopCustomersAsync_WithCustomDates_ShouldUseProvidedDates()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var startDate = new DateTime(2024, 2, 1);
            var endDate = new DateTime(2024, 2, 28);

            var topCustomers = new List<TopCustomerDto>
            {
                new TopCustomerDto
                {
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer A",
                    CustomerEmail = "customer@example.com",
                    TotalSpent = 5000,
                    OrderCount = 3
                }
            };

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, startDate, endDate, 5))
                .ReturnsAsync(topCustomers);

            // Act
            var result = await _service.GetTopCustomersAsync(providerId, "month", startDate, endDate, 5);

            // Assert
            Assert.Single(result);
            _revenueRepo.Verify(r => r.GetTopCustomersAsync(providerId, startDate, endDate, 5), Times.Once);
        }

        [Fact]
        public async Task UTCID13_GetTopCustomersAsync_WeekPeriod_ShouldCalculateWeekRange()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var topCustomers = new List<TopCustomerDto>();

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
                .ReturnsAsync(topCustomers);

            // Act
            await _service.GetTopCustomersAsync(providerId, "week", null, null, 5);

            // Assert
            _revenueRepo.Verify(r => r.GetTopCustomersAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                5), Times.Once);
        }

        [Fact]
        public async Task UTCID14_GetTopCustomersAsync_YearPeriod_ShouldCalculateYearRange()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var topCustomers = new List<TopCustomerDto>();

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
                .ReturnsAsync(topCustomers);

            // Act
            await _service.GetTopCustomersAsync(providerId, "year", null, null, 5);

            // Assert
            _revenueRepo.Verify(r => r.GetTopCustomersAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                5), Times.Once);
        }

        [Fact]
        public async Task UTCID15_GetTopCustomersAsync_EmptyResult_ShouldReturnEmptyList()
        {
            // Arrange
            var providerId = Guid.NewGuid();

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 5))
                .ReturnsAsync(new List<TopCustomerDto>());

            // Act
            var result = await _service.GetTopCustomersAsync(providerId, "month", null, null, 5);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task UTCID16_GetTopCustomersAsync_CustomLimit_ShouldRespectLimit()
        {
            // Arrange
            var providerId = Guid.NewGuid();
            var limit = 10;

            var topCustomers = new List<TopCustomerDto>();

            _revenueRepo.Setup(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), limit))
                .ReturnsAsync(topCustomers);

            // Act
            await _service.GetTopCustomersAsync(providerId, "month", null, null, limit);

            // Assert
            _revenueRepo.Verify(r => r.GetTopCustomersAsync(providerId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), limit), Times.Once);
        }

        #endregion

    }
}

