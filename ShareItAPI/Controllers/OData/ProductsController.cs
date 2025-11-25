using BusinessObject.DTOs.ProductDto;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.Caching.Memory;
using Services.ProductServices;
using System.Security.Claims;

namespace ShareItAPI.Controllers.OData
{
    [Route("odata/products")]
    [ApiController]
    [AllowAnonymous]
    public class ProductsController : ODataController
    {
        private readonly IProductService _productService;
        private readonly ShareItDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IProductService productService, ShareItDbContext context, IMemoryCache cache, ILogger<ProductsController> logger)
        {
            _productService = productService;
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [EnableQuery(PageSize = 20, MaxExpansionDepth = 2, MaxTop = 100)]
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                _logger.LogInformation("OData Products request started");
                var startTime = DateTime.UtcNow;
                
                // If user is authenticated and is a Provider, return ALL products (including archived, pending, etc.)
                // Otherwise, return only available products (for customers/public)
                IQueryable<ProductDTO> query;
                if (User.Identity?.IsAuthenticated == true && User.IsInRole("provider"))
                {
                    query = _productService.GetAllNoFilter();
                }
                else
                {
                    query = _productService.GetAll();
                }
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("OData Products request completed in {Duration}ms", duration.TotalMilliseconds);
                
                return Ok(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OData Products request");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
