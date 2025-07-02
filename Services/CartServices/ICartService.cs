using BusinessObject.DTOs.CartDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.CartServices
{
    public interface ICartService
    {
        Task<CartDto> GetUserCartAsync(Guid customerId);
        Task<bool> AddProductToCartAsync(Guid customerId, CartAddRequestDto cartItemDto);
        Task<bool> UpdateCartItemAsync(Guid customerId, Guid cartItemId, CartUpdateRequestDto updateDto);
        Task<bool> RemoveCartItemAsync(Guid customerId, Guid cartItemId);
        Task<int> GetCartItemCountAsync(Guid customerId);
    }
}
