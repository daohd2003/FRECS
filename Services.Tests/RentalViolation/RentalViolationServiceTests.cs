using AutoMapper;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Repositories.OrderRepositories;
using Repositories.ProductRepositories;
using Repositories.RentalViolationRepositories;
using Services.CloudServices;
using Services.NotificationServices;
using Services.RentalViolationServices;
using System.Collections.Generic;
using System.Linq;

namespace Services.Tests.RentalViolationTests
{
    public class RentalViolationServiceTests
    {
        private readonly Mock<IRentalViolationRepository> _violationRepo;
        private readonly Mock<IOrderRepository> _orderRepo;
        private readonly Mock<IProductRepository> _productRepo;
        private readonly Mock<ICloudinaryService> _cloudService;
        private readonly Mock<INotificationService> _notificationService;
        private readonly Mock<IMapper> _mapper;
        private readonly ShareItDbContext _context;
        private readonly RentalViolationService _service;

        public RentalViolationServiceTests()
        {
            _violationRepo = new Mock<IRentalViolationRepository>();
            _orderRepo = new Mock<IOrderRepository>();
            _productRepo = new Mock<IProductRepository>();
            _cloudService = new Mock<ICloudinaryService>();
            _notificationService = new Mock<INotificationService>();
            _mapper = new Mock<IMapper>();

            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<ShareItDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ShareItDbContext(options);

            _service = new RentalViolationService(
                _violationRepo.Object,
                _orderRepo.Object,
                _productRepo.Object,
                _cloudService.Object,
                _notificationService.Object,
                _mapper.Object,
                _context);
        }

        #region CreateMultipleViolationsAsync

        [Fact]
        public async Task UTCID01_CreateMultipleViolations_ShouldPersistAndNotify()
        {
            var providerId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                ProviderId = providerId,
                CustomerId = Guid.NewGuid(),
                Status = OrderStatus.returning
            };
            var orderItem = new OrderItem
            {
                Id = itemId,
                OrderId = orderId,
                Order = order
            };
            order.Items = new List<OrderItem> { orderItem };

            _orderRepo.Setup(r => r.GetOrderWithItemsAsync(orderId)).ReturnsAsync(order);
            _violationRepo.Setup(v => v.GetViolationsByOrderItemIdAsync(itemId))
                .ReturnsAsync(new List<BusinessObject.Models.RentalViolation>());
            _violationRepo.Setup(v => v.AddAsync(It.IsAny<BusinessObject.Models.RentalViolation>()))
                .Returns(Task.CompletedTask);
            _notificationService.Setup(n => n.SendNotification(
                    order.CustomerId,
                    It.IsAny<string>(),
                    NotificationType.order,
                    order.Id))
                .Returns(Task.CompletedTask);
            _orderRepo.Setup(r => r.UpdateAsync(order)).ReturnsAsync(true);

            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = orderId,
                Violations = new List<CreateRentalViolationDto>
                {
                    new CreateRentalViolationDto
                    {
                        OrderItemId = itemId,
                        ViolationType = ViolationType.DAMAGED,
                        Description = new string('a', 20),
                        PenaltyPercentage = 10,
                        PenaltyAmount = 100,
                        EvidenceFiles = new List<Microsoft.AspNetCore.Http.IFormFile>()
                    }
                }
            };

            var result = await _service.CreateMultipleViolationsAsync(dto, providerId);

            Assert.Single(result);
            Assert.Equal(OrderStatus.returned_with_issue, order.Status);
            _violationRepo.Verify(v => v.AddAsync(It.IsAny<BusinessObject.Models.RentalViolation>()), Times.Once);
            _notificationService.Verify(n => n.SendNotification(order.CustomerId, It.IsAny<string>(), NotificationType.order, order.Id), Times.Once);
        }

        [Fact]
        public async Task UTCID02_CreateMultipleViolations_OrderOwnedByAnotherProvider_ShouldThrow()
        {
            var providerId = Guid.NewGuid();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                ProviderId = Guid.NewGuid(),
                Status = OrderStatus.returning,
                Items = new List<OrderItem>()
            };
            _orderRepo.Setup(r => r.GetOrderWithItemsAsync(order.Id)).ReturnsAsync(order);
            var dto = new CreateMultipleViolationsRequestDto
            {
                OrderId = order.Id,
                Violations = new List<CreateRentalViolationDto>()
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CreateMultipleViolationsAsync(dto, providerId));
        }

        #endregion

        #region GetProviderViolationsAsync

        [Fact]
        public async Task UTCID03_GetProviderViolations_ShouldMapResults()
        {
            var providerId = Guid.NewGuid();
            var violations = new List<BusinessObject.Models.RentalViolation>
            {
                new BusinessObject.Models.RentalViolation { ViolationId = Guid.NewGuid(), OrderItem = new OrderItem() }
            };
            var dtoList = new List<RentalViolationDto>
            {
                new RentalViolationDto { ViolationId = violations[0].ViolationId }
            };

            _violationRepo.Setup(v => v.GetViolationsByProviderIdAsync(providerId))
                .ReturnsAsync(violations);
            _mapper.Setup(m => m.Map<IEnumerable<RentalViolationDto>>(violations))
                .Returns(dtoList);

            var result = await _service.GetProviderViolationsAsync(providerId);

            Assert.Single(result);
            Assert.Equal(violations[0].ViolationId, result.First().ViolationId);
        }

        #endregion

        #region UpdateViolationByProviderAsync

        [Fact]
        public async Task UTCID04_UpdateViolation_ValidState_ShouldResetToPending()
        {
            var providerId = Guid.NewGuid();
            var violationId = Guid.NewGuid();
            var order = new Order { ProviderId = providerId };
            var orderItem = new OrderItem { Order = order };
            var violation = new BusinessObject.Models.RentalViolation
            {
                ViolationId = violationId,
                Status = ViolationStatus.CUSTOMER_REJECTED,
                OrderItem = orderItem
            };

            _violationRepo.Setup(v => v.GetViolationWithDetailsAsync(violationId))
                .ReturnsAsync(violation);
            _violationRepo.Setup(v => v.UpdateViolationAsync(violation)).ReturnsAsync(true);

            var dto = new UpdateViolationDto { Description = "Fix" };

            var result = await _service.UpdateViolationByProviderAsync(violationId, dto, providerId);

            Assert.True(result);
            Assert.Equal(ViolationStatus.PENDING, violation.Status);
            _violationRepo.Verify(v => v.UpdateViolationAsync(violation), Times.Once);
        }

        [Fact]
        public async Task UTCID05_UpdateViolation_NotProvider_ShouldThrowUnauthorized()
        {
            var violationId = Guid.NewGuid();
            var violation = new BusinessObject.Models.RentalViolation
            {
                ViolationId = violationId,
                Status = ViolationStatus.CUSTOMER_REJECTED,
                OrderItem = new OrderItem { Order = new Order { ProviderId = Guid.NewGuid() } }
            };
            _violationRepo.Setup(v => v.GetViolationWithDetailsAsync(violationId))
                .ReturnsAsync(violation);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.UpdateViolationByProviderAsync(violationId, new UpdateViolationDto(), Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID06_UpdateViolation_InvalidStatus_ShouldThrowInvalidOperation()
        {
            var providerId = Guid.NewGuid();
            var violationId = Guid.NewGuid();
            var violation = new BusinessObject.Models.RentalViolation
            {
                ViolationId = violationId,
                Status = ViolationStatus.PENDING,
                OrderItem = new OrderItem { Order = new Order { ProviderId = providerId } }
            };

            _violationRepo.Setup(v => v.GetViolationWithDetailsAsync(violationId))
                .ReturnsAsync(violation);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateViolationByProviderAsync(violationId, new UpdateViolationDto(), providerId));
        }

        #endregion
    }
}

