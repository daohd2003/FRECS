using BusinessObject.DTOs.ApiResponses;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.UserServices;
using Services.Utilities;
using System.Security.Claims;

namespace ShareItAPI.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(new ApiResponse<object>("Success", users));
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<User>("Success", user));
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            await _userService.AddAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new ApiResponse<User>("User created successfully", user));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "admin,customer,provider")]
        public async Task<IActionResult> Update(Guid id, User user)
        {
            var validation = GuidUtilities.ValidateGuid(id.ToString(), user.Id);
            if (!validation.IsValid)
            {
                return BadRequest(new ApiResponse<string>(validation.ErrorMessage, null));
            }

            // Mã hóa mật khẩu (giả sử user.PasswordHash chứa mật khẩu plain text)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            var result = await _userService.UpdateAsync(user);
            if (!result)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<string>("User updated successfully", null));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _userService.DeleteAsync(id);
            if (!result)
                return NotFound(new ApiResponse<string>("User not found", null));

            return Ok(new ApiResponse<string>("User deleted successfully", null));
        }
    }
}