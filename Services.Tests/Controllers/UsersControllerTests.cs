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
	}
}
