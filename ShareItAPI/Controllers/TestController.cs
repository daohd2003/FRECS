using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [Route("api/test")]
    [ApiController]
    [AllowAnonymous] // Allow anonymous access for testing
    public class TestController : ControllerBase
    {
        private readonly ShareItDbContext _context;

        public TestController(ShareItDbContext context)
        {
            _context = context;
        }

        [HttpGet("category")]
        public async Task<IActionResult> TestCreateCategory()
        {
            try
            {
                var newCategory = new BusinessObject.Models.Category
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Category " + DateTime.UtcNow.Ticks,
                    Description = "This is a test description.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ImageUrl = "https://example.com/test-image.jpg"
                };

                _context.Categories.Add(newCategory);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Test category created successfully", category = newCategory });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Failed to create test category", 
                    error = ex.Message,
                    innerException = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("database")]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                // Simple test first
                var categoryCount = await _context.Categories.CountAsync();
                
                return Ok(new { 
                    message = "Database connection successful", 
                    categoryCount = categoryCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    message = "Database connection failed", 
                    error = ex.Message,
                    innerException = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("simple")]
        public IActionResult TestSimple()
        {
            return Ok(new { 
                message = "API is working", 
                timestamp = DateTime.UtcNow 
            });
        }
    }
}
