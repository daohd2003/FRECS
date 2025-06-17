using BusinessObject.DTOs.UsersDto;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Services.UserServices;

namespace ShareItAPI.Controllers.OData
{
    [Route("odata/list/users")]
    [ApiController]
    [AllowAnonymous]
    public class UsersController : ODataController
    {
        private readonly IUserService _userService;
        private readonly ShareItDbContext _context;

        public UsersController(IUserService userService, ShareItDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        [EnableQuery]
        [HttpGet]
        public IActionResult Get()
        {
            /*var query = _userService.GetAllAsync();*/
            /*    return Ok(query);*/
            return Ok(_context.Users.Select(u => new UserODataDTO
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role, // Đảm bảo thuộc tính Role trong User cũng là UserRole hoặc có thể ánh xạ được
                CreatedAt = u.CreatedAt,
                IsActive = u.IsActive
            }).AsQueryable());
        }
    }
}
