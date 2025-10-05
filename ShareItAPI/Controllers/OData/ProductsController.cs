using BusinessObject.DTOs.ProductDto;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Services.ProductServices;

namespace ShareItAPI.Controllers.OData
{
    [Route("odata/products")]
    [ApiController]
    [AllowAnonymous]
    public class ProductsController : ODataController
    {
        private readonly IProductService _productService;
        private readonly ShareItDbContext _context;

        public ProductsController(IProductService productService, ShareItDbContext context)
        {
            _productService = productService;
            _context = context;
        }

        [EnableQuery(PageSize = 20, MaxExpansionDepth = 2, MaxTop = 100)]
        [HttpGet]
        public IActionResult Get()
        {
            var query = _productService.GetAll();
            return Ok(query);
        }
    }
}
