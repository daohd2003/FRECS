using AutoMapper;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Repositories.DiscountCodeRepositories;
using Services.DiscountCodeServices;

namespace Services.Tests.DiscountCode
{

    public class CreateDiscountCodeServiceTests
    {
        private readonly Mock<IDiscountCodeRepository> _mockDiscountCodeRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly DiscountCodeService _discountCodeService;

        public CreateDiscountCodeServiceTests()
        {
            _mockDiscountCodeRepository = new Mock<IDiscountCodeRepository>();
            _mockMapper = new Mock<IMapper>();

            _discountCodeService = new DiscountCodeService(
                _mockDiscountCodeRepository.Object,
                _mockMapper.Object
            );
        }

        /// <summary>
        /// UTCID01: Create discount code with valid, unique code
        /// Expected: Successfully create discount code and return DiscountCodeDto
        /// Backend: No exception, successful creation
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateDiscountCode_ValidUniqueCode_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var discountCodeEntity = new BusinessObject.Models.DiscountCode
            {
                Id = Guid.NewGuid(),
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountCodeEntity.Id,
                Code = "SUMMER25",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = discountCodeEntity.CreatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.DiscountCode>(createDto))
                .Returns(discountCodeEntity);

            _mockDiscountCodeRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(discountCodeEntity))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.CreateDiscountCodeAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SUMMER25", result.Code);
            Assert.Equal(DiscountType.Percentage, result.DiscountType);
            Assert.Equal(25, result.Value);
            Assert.Equal(100, result.Quantity);
            Assert.Equal(0, result.UsedCount);
            Assert.Equal(DiscountStatus.Active, result.Status);
            Assert.Equal(DiscountUsageType.Purchase, result.UsageType);

            // Verify repository was called
            _mockDiscountCodeRepository.Verify(x => x.IsCodeUniqueAsync(createDto.Code, null), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Once);
        }

        /// <summary>
        /// UTCID02: Create discount code with duplicate code
        /// Expected: Throw InvalidOperationException with message "Discount code '{code}' already exists."
        /// Backend: Service validates uniqueness and throws exception
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateDiscountCode_DuplicateCode_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "WINTER10",
                DiscountType = DiscountType.Percentage,
                Value = 10,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 50,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            // Setup mock - code is NOT unique (already exists)
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(false);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _discountCodeService.CreateDiscountCodeAsync(createDto)
            );

            // Verify the exact exception message
            Assert.Equal("Discount code 'WINTER10' already exists.", exception.Message);

            // Verify repository was called to check uniqueness
            _mockDiscountCodeRepository.Verify(x => x.IsCodeUniqueAsync(createDto.Code, null), Times.Once);
            
            // Verify AddAsync was NOT called since validation failed
            _mockDiscountCodeRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Never);
        }

        /// <summary>
        /// UTCID05: Create discount code with past expiration date
        /// Expected: Throw InvalidOperationException with message "Expiration date must be in the future."
        /// Backend: Service validates expiration date and throws exception
        /// </summary>
        [Fact]
        public async Task UTCID05_CreateDiscountCode_PastExpirationDate_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "PASTDATE",
                DiscountType = DiscountType.Percentage,
                Value = 25,
                ExpirationDate = DateTime.UtcNow.AddDays(-1), // Past date
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            // Setup mock - code is unique
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _discountCodeService.CreateDiscountCodeAsync(createDto)
            );

            // Verify the exact exception message
            Assert.Equal("Expiration date must be in the future.", exception.Message);

            // Verify repository was called to check uniqueness but not to add
            _mockDiscountCodeRepository.Verify(x => x.IsCodeUniqueAsync(createDto.Code, null), Times.Once);
            _mockDiscountCodeRepository.Verify(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()), Times.Never);
        }

        /// <summary>
        /// Additional Test: Create discount code with Fixed discount type
        /// Expected: Successfully create discount code with Fixed type
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_ValidFixedType_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "FIXED50",
                DiscountType = DiscountType.Fixed,
                Value = 50,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var discountCodeEntity = new BusinessObject.Models.DiscountCode
            {
                Id = Guid.NewGuid(),
                Code = "FIXED50",
                DiscountType = DiscountType.Fixed,
                Value = 50,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountCodeEntity.Id,
                Code = "FIXED50",
                DiscountType = DiscountType.Fixed,
                Value = 50,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = discountCodeEntity.CreatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.DiscountCode>(createDto))
                .Returns(discountCodeEntity);

            _mockDiscountCodeRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(discountCodeEntity))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.CreateDiscountCodeAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("FIXED50", result.Code);
            Assert.Equal(DiscountType.Fixed, result.DiscountType);
            Assert.Equal(50, result.Value);
        }

        /// <summary>
        /// Additional Test: Create discount code with Rental usage type
        /// Expected: Successfully create discount code for Rental usage
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_RentalUsageType_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "RENT20",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 50,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Rental
            };

            var discountCodeEntity = new BusinessObject.Models.DiscountCode
            {
                Id = Guid.NewGuid(),
                Code = "RENT20",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Rental,
                CreatedAt = DateTime.UtcNow
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountCodeEntity.Id,
                Code = "RENT20",
                DiscountType = DiscountType.Percentage,
                Value = 20,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 50,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Rental,
                CreatedAt = discountCodeEntity.CreatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.DiscountCode>(createDto))
                .Returns(discountCodeEntity);

            _mockDiscountCodeRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(discountCodeEntity))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.CreateDiscountCodeAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("RENT20", result.Code);
            Assert.Equal(DiscountUsageType.Rental, result.UsageType);
        }

        /// <summary>
        /// Additional Test: Create discount code at exact boundary - expiration date is just after now
        /// Expected: Successfully create discount code
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_ExpirationDateJustAfterNow_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var futureDate = DateTimeHelper.GetVietnamTime().AddMinutes(1); // Just 1 minute in the future (Vietnam time)
            
            var createDto = new CreateDiscountCodeDto
            {
                Code = "BOUNDARY",
                DiscountType = DiscountType.Percentage,
                Value = 15,
                ExpirationDate = futureDate,
                Quantity = 10,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase
            };

            var discountCodeEntity = new BusinessObject.Models.DiscountCode
            {
                Id = Guid.NewGuid(),
                Code = "BOUNDARY",
                DiscountType = DiscountType.Percentage,
                Value = 15,
                ExpirationDate = futureDate,
                Quantity = 10,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountCodeEntity.Id,
                Code = "BOUNDARY",
                DiscountType = DiscountType.Percentage,
                Value = 15,
                ExpirationDate = futureDate,
                Quantity = 10,
                UsedCount = 0,
                Status = DiscountStatus.Active,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = discountCodeEntity.CreatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.DiscountCode>(createDto))
                .Returns(discountCodeEntity);

            _mockDiscountCodeRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(discountCodeEntity))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.CreateDiscountCodeAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("BOUNDARY", result.Code);
            Assert.True(result.ExpirationDate > DateTimeHelper.GetVietnamTime());
        }

        /// <summary>
        /// Additional Test: Create discount code with Inactive status
        /// Expected: Successfully create discount code with Inactive status
        /// </summary>
        [Fact]
        public async Task CreateDiscountCode_InactiveStatus_ShouldReturnDiscountCodeDto()
        {
            // Arrange
            var createDto = new CreateDiscountCodeDto
            {
                Code = "INACTIVE",
                DiscountType = DiscountType.Percentage,
                Value = 30,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Quantity = 100,
                Status = DiscountStatus.Inactive,
                UsageType = DiscountUsageType.Purchase
            };

            var discountCodeEntity = new BusinessObject.Models.DiscountCode
            {
                Id = Guid.NewGuid(),
                Code = "INACTIVE",
                DiscountType = DiscountType.Percentage,
                Value = 30,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Inactive,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = DateTime.UtcNow
            };

            var expectedDto = new DiscountCodeDto
            {
                Id = discountCodeEntity.Id,
                Code = "INACTIVE",
                DiscountType = DiscountType.Percentage,
                Value = 30,
                ExpirationDate = createDto.ExpirationDate,
                Quantity = 100,
                UsedCount = 0,
                Status = DiscountStatus.Inactive,
                UsageType = DiscountUsageType.Purchase,
                CreatedAt = discountCodeEntity.CreatedAt
            };

            // Setup mocks
            _mockDiscountCodeRepository.Setup(x => x.IsCodeUniqueAsync(createDto.Code, null))
                .ReturnsAsync(true);

            _mockMapper.Setup(x => x.Map<BusinessObject.Models.DiscountCode>(createDto))
                .Returns(discountCodeEntity);

            _mockDiscountCodeRepository.Setup(x => x.AddAsync(It.IsAny<BusinessObject.Models.DiscountCode>()))
                .Returns(Task.CompletedTask);

            _mockMapper.Setup(x => x.Map<DiscountCodeDto>(discountCodeEntity))
                .Returns(expectedDto);

            // Act
            var result = await _discountCodeService.CreateDiscountCodeAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("INACTIVE", result.Code);
            Assert.Equal(DiscountStatus.Inactive, result.Status);
        }
    }
}

