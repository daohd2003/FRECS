using BusinessObject.DTOs.ApiResponses;
using BusinessObject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.UserServices;
using ShareItAPI.Controllers;
using System.Security.Claims;
using Xunit;

namespace Services.Tests.Controllers
{
	public class UsersControllerTests
	{
		private readonly Mock<IUserService> _mockUserService;
		private readonly UsersController _controller;

		public UsersControllerTests()
		{
			_mockUserService = new Mock<IUserService>();
			_controller = new UsersController(_mockUserService.Object);

			// Simulate a 'staff' user principal
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Role, "staff"),
				new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
			};
			var identity = new ClaimsIdentity(claims, "TestAuth");
			var principal = new ClaimsPrincipal(identity);
			_controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = principal }
			};
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetAll_AsStaff_WithUsers_ReturnsOkWithSuccessMessage()
		{
			// Arrange
			var users = new List<User>
			{
				new User { Id = Guid.NewGuid(), Email = "u1@example.com" },
				new User { Id = Guid.NewGuid(), Email = "u2@example.com" }
			};
			_mockUserService.Setup(s => s.GetAllAsync()).ReturnsAsync(users);

			// Act
			var result = await _controller.GetAll();

			// Assert
			var ok = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(200, ok.StatusCode);
			var api = Assert.IsType<ApiResponse<object>>(ok.Value);
			Assert.Equal("Success", api.Message);
			var returned = Assert.IsAssignableFrom<IEnumerable<User>>(api.Data);
			Assert.Equal(2, returned.Count());
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetAll_AsStaff_NoUsers_ReturnsOkWithEmptyList()
		{
			// Arrange
			_mockUserService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<User>());

			// Act
			var result = await _controller.GetAll();

			// Assert
			var ok = Assert.IsType<OkObjectResult>(result);
			var api = Assert.IsType<ApiResponse<object>>(ok.Value);
			Assert.Equal("Success", api.Message);
			var returned = Assert.IsAssignableFrom<IEnumerable<User>>(api.Data);
			Assert.Empty(returned);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetAll_AsStaff_ServiceThrows_PropagatesException()
		{
			// Arrange
			var exMessage = "DB down";
			_mockUserService.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception(exMessage));

			// Act & Assert
			var ex = await Assert.ThrowsAsync<Exception>(() => _controller.GetAll());
			Assert.Equal(exMessage, ex.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetById_AsCustomer_UserExists_ReturnsOkWithSuccessMessage()
		{
			// Arrange
			var id = Guid.NewGuid();
			var user = new User { Id = id, Email = "view@example.com" };
			_mockUserService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(user);

			// Create a controller with an allowed role for this action (customer)
			var customerController = new UsersController(_mockUserService.Object);
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Role, "customer"),
				new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
			};
			var identity = new ClaimsIdentity(claims, "TestAuth");
			var principal = new ClaimsPrincipal(identity);
			customerController.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = principal }
			};

			// Act
			var result = await customerController.GetById(id);

			// Assert
			var ok = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(200, ok.StatusCode);
			var api = Assert.IsType<ApiResponse<User>>(ok.Value);
			Assert.Equal("Success", api.Message);
			Assert.Equal(id, api.Data.Id);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetById_AsCustomer_UserNotFound_ReturnsNotFoundWithMessage()
		{
			// Arrange
			var id = Guid.NewGuid();
			_mockUserService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((User?)null);

			var customerController = new UsersController(_mockUserService.Object);
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Role, "customer"),
				new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
			};
			var identity = new ClaimsIdentity(claims, "TestAuth");
			var principal = new ClaimsPrincipal(identity);
			customerController.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = principal }
			};

			// Act
			var result = await customerController.GetById(id);

			// Assert
			var notFound = Assert.IsType<NotFoundObjectResult>(result);
			Assert.Equal(404, notFound.StatusCode);
			var api = Assert.IsType<ApiResponse<string>>(notFound.Value);
			Assert.Equal("User not found", api.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "View")]
		public async Task GetById_AsCustomer_ServiceThrows_PropagatesException()
		{
			// Arrange
			var id = Guid.NewGuid();
			var exMessage = "Boom";
			_mockUserService.Setup(s => s.GetByIdAsync(id)).ThrowsAsync(new Exception(exMessage));

			var customerController = new UsersController(_mockUserService.Object);
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Role, "customer"),
				new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
			};
			var identity = new ClaimsIdentity(claims, "TestAuth");
			var principal = new ClaimsPrincipal(identity);
			customerController.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = principal }
			};

			// Act & Assert
			var ex = await Assert.ThrowsAsync<Exception>(() => customerController.GetById(id));
			Assert.Equal(exMessage, ex.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Block")]
		public async Task BlockUser_AsStaff_UserExists_ReturnsOkWithMessage()
		{
			// Arrange
			var userId = Guid.NewGuid();
			_mockUserService.Setup(s => s.BlockUserAsync(userId)).ReturnsAsync(true);

			// Act
			var result = await _controller.BlockUser(userId);

			// Assert
			var ok = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(200, ok.StatusCode);
			var api = Assert.IsType<ApiResponse<string>>(ok.Value);
			Assert.Equal("User blocked (set inactive) successfully", api.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Block")]
		public async Task BlockUser_AsStaff_UserNotFound_ReturnsNotFoundWithMessage()
		{
			// Arrange
			var userId = Guid.NewGuid();
			_mockUserService.Setup(s => s.BlockUserAsync(userId)).ReturnsAsync(false);

			// Act
			var result = await _controller.BlockUser(userId);

			// Assert
			var notFound = Assert.IsType<NotFoundObjectResult>(result);
			Assert.Equal(404, notFound.StatusCode);
			var api = Assert.IsType<ApiResponse<string>>(notFound.Value);
			Assert.Equal("User not found", api.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Block")]
		public async Task BlockUser_AsStaff_ServiceThrows_PropagatesException()
		{
			// Arrange
			var userId = Guid.NewGuid();
			var exMessage = "Unexpected failure";
			_mockUserService.Setup(s => s.BlockUserAsync(userId)).ThrowsAsync(new Exception(exMessage));

			// Act & Assert
			var ex = await Assert.ThrowsAsync<Exception>(() => _controller.BlockUser(userId));
			Assert.Equal(exMessage, ex.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Unblock")]
		public async Task UnblockUser_AsStaff_UserExists_ReturnsOkWithMessage()
		{
			// Arrange
			var userId = Guid.NewGuid();
			_mockUserService.Setup(s => s.UnblockUserAsync(userId)).ReturnsAsync(true);

			// Act
			var result = await _controller.UnblockUser(userId);

			// Assert
			var ok = Assert.IsType<OkObjectResult>(result);
			Assert.Equal(200, ok.StatusCode);
			var api = Assert.IsType<ApiResponse<string>>(ok.Value);
			Assert.Equal("User unblocked (set active) successfully", api.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Unblock")]
		public async Task UnblockUser_AsStaff_UserNotFound_ReturnsNotFoundWithMessage()
		{
			// Arrange
			var userId = Guid.NewGuid();
			_mockUserService.Setup(s => s.UnblockUserAsync(userId)).ReturnsAsync(false);

			// Act
			var result = await _controller.UnblockUser(userId);

			// Assert
			var notFound = Assert.IsType<NotFoundObjectResult>(result);
			Assert.Equal(404, notFound.StatusCode);
			var api = Assert.IsType<ApiResponse<string>>(notFound.Value);
			Assert.Equal("User not found", api.Message);
		}

		[Fact]
		[Trait("Feature", "Users")]
		[Trait("Action", "Unblock")]
		public async Task UnblockUser_AsStaff_ServiceThrows_PropagatesException()
		{
			// Arrange
			var userId = Guid.NewGuid();
			var exMessage = "Unexpected failure";
			_mockUserService.Setup(s => s.UnblockUserAsync(userId)).ThrowsAsync(new Exception(exMessage));

			// Act & Assert
			var ex = await Assert.ThrowsAsync<Exception>(() => _controller.UnblockUser(userId));
			Assert.Equal(exMessage, ex.Message);
		}
	}
}
