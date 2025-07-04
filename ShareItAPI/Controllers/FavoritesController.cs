using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.FavoriteDtos;
using BusinessObject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.FavoriteServices;
using System;
using System.Threading.Tasks;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/favorites")]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly IFavoriteService _favoriteService;

        public FavoritesController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetFavorites(Guid userId)
        {
            var favorites = await _favoriteService.GetFavoritesByUserIdAsync(userId);
            return Ok(new ApiResponse<List<Favorite>>("Get favorites list successfully", favorites));
        }

        [HttpGet("check")]
        public async Task<IActionResult> IsFavorite(Guid userId, Guid productId)
        {
            var isFav = await _favoriteService.IsFavoriteAsync(userId, productId);
            return Ok(new ApiResponse<bool>("Favorite check successful", isFav));
        }

        [HttpPost]
        public async Task<IActionResult> AddFavorite([FromBody] FavoriteCreateDto dto)
        {
            bool alreadyExists = await _favoriteService.IsFavoriteAsync(dto.UserId, dto.ProductId);
            if (alreadyExists)
            {
                return BadRequest(new ApiResponse<string>("This product is already in favorites.", null));
            }

            var favorite = new Favorite
            {
                UserId = dto.UserId,
                ProductId = dto.ProductId
            };

            await _favoriteService.AddFavoriteAsync(favorite);
            return Ok(new ApiResponse<string>("Added to favorites successfully", null));
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveFavorite(Guid userId, Guid productId)
        {
            await _favoriteService.RemoveFavoriteAsync(userId, productId);
            return Ok(new ApiResponse<string>("Removed from favorites successfully", null));
        }
    }
}