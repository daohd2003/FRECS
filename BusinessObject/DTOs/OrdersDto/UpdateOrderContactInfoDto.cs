using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.OrdersDto
{
    public class UpdateOrderContactInfoDto
    {
        public Guid OrderId { get; set; }

        public string CustomerFullName { get; set; } = string.Empty;

        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public bool HasAgreedToPolicies { get; set; }
    }
}
