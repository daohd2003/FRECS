using BusinessObject.DTOs.OrdersDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class OrderItemProfile : AutoMapper.Profile
    {
        public OrderItemProfile()
        {
            CreateMap<OrderItem, OrderItemDto>().ReverseMap();

            CreateMap<CreateOrderItemDto, OrderItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Order, opt => opt.Ignore())
                .ForMember(dest => dest.OrderId, opt => opt.Ignore());
        }
    }
}
