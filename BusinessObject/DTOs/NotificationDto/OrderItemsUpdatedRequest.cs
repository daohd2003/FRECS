using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.NotificationDto
{
    public class OrderItemsUpdatedRequest
    {
        public Guid OrderId { get; set; }
        public IEnumerable<Guid> UpdatedItemIds { get; set; }
    }
}
