using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.StaffDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Authentication;
using Services.UserServices;
using System.Text.Json;

namespace ShareItAPI.Controllers
{
    [Route("api/staff")]
    [ApiController]
    [Authorize(Roles = "admin")]
    public class StaffController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtService _jwtService;

        public StaffController(IUserService userService, IJwtService jwtService)
        {
            _userService = userService;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Lấy tất cả staff accounts
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllStaff()
        {
            try
            {
                var users = await _userService.GetAllAsync();
                var staffUsers = users.Where(u => u.Role == UserRole.staff).ToList();

                var staffViewModels = staffUsers.Select(s => new StaffViewModel
                {
                    Id = s.Id,
                    FullName = s.Profile?.FullName ?? "",
                    Email = s.Email,
                    IsActive = s.IsActive ?? true,
                    CreatedAt = s.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<IEnumerable<StaffViewModel>>("Success", staffViewModels));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Lấy staff account theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStaffById(Guid id)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null || user.Role != UserRole.staff)
                    return NotFound(new ApiResponse<string>("Staff not found", null));

                var staffViewModel = new StaffViewModel
                {
                    Id = user.Id,
                    FullName = user.Profile?.FullName ?? "",
                    Email = user.Email,
                    IsActive = user.IsActive ?? true,
                    CreatedAt = user.CreatedAt
                };

                return Ok(new ApiResponse<StaffViewModel>("Success", staffViewModel));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Tạo staff account mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>("Validation failed", ModelState));
                }

                // Kiểm tra email đã tồn tại chưa
                var existingUser = await _userService.GetUserByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new ApiResponse<string>("Email already exists", null));
                }

                // Tạo user mới với role staff
                var newStaff = new User
                {
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = UserRole.staff,
                    IsActive = request.IsActive,
                    EmailConfirmed = true, // Staff account không cần verify email
                    CreatedAt = DateTimeHelper.GetVietnamTime()
                };

                // Tạo profile cho staff
                newStaff.Profile = new Profile
                {
                    FullName = request.FullName,
                    ProfilePictureUrl = "https://inkythuatso.com/uploads/thumbnails/800/2023/03/3-anh-dai-dien-trang-inkythuatso-03-15-25-56.jpg"
                };

                await _userService.AddAsync(newStaff);

                var staffViewModel = new StaffViewModel
                {
                    Id = newStaff.Id,
                    FullName = newStaff.Profile.FullName,
                    Email = newStaff.Email,
                    IsActive = newStaff.IsActive ?? true,
                    CreatedAt = newStaff.CreatedAt
                };

                return CreatedAtAction(nameof(GetStaffById), new { id = newStaff.Id }, 
                    new ApiResponse<StaffViewModel>("Staff created successfully", staffViewModel));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Cập nhật thông tin staff
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>("Validation failed", ModelState));
                }

                var user = await _userService.GetByIdAsync(id);
                if (user == null || user.Role != UserRole.staff)
                    return NotFound(new ApiResponse<string>("Staff not found", null));

                // Kiểm tra email mới có trùng với user khác không
                if (user.Email != request.Email)
                {
                    var existingUser = await _userService.GetUserByEmailAsync(request.Email);
                    if (existingUser != null && existingUser.Id != id)
                    {
                        return BadRequest(new ApiResponse<string>("Email already exists", null));
                    }
                    user.Email = request.Email;
                }

                user.IsActive = request.IsActive;

                if (user.Profile != null)
                {
                    user.Profile.FullName = request.FullName;
                }

                await _userService.UpdateAsync(user);

                var staffViewModel = new StaffViewModel
                {
                    Id = user.Id,
                    FullName = user.Profile?.FullName ?? "",
                    Email = user.Email,
                    IsActive = user.IsActive ?? true,
                    CreatedAt = user.CreatedAt
                };

                return Ok(new ApiResponse<StaffViewModel>("Staff updated successfully", staffViewModel));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Xóa staff account
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStaff(Guid id)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null || user.Role != UserRole.staff)
                    return NotFound(new ApiResponse<string>("Staff not found", null));

                var result = await _userService.DeleteAsync(id);
                if (!result)
                    return BadRequest(new ApiResponse<string>("Failed to delete staff", null));

                return Ok(new ApiResponse<string>("Staff deleted successfully", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Gửi email reset password cho staff
        /// </summary>
        [HttpPost("{id}/send-password-reset")]
        public async Task<IActionResult> SendPasswordReset(Guid id)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null || user.Role != UserRole.staff)
                    return NotFound(new ApiResponse<string>("Staff not found", null));

                var result = await _jwtService.ForgotPasswordAsync(user.Email);
                if (!result)
                    return BadRequest(new ApiResponse<string>("Failed to send password reset email", null));

                return Ok(new ApiResponse<string>("Password reset link sent to email", null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>($"Error: {ex.Message}", null));
            }
        }
    }
}

