using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.FavoriteDtos
{
    public class FavoriteCreateDto
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
    }
}
