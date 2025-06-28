using AutoMapper;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class OrderProfile : AutoMapper.Profile
    {
        public OrderProfile()
        {
            CreateMap<Order, OrderDto>().ReverseMap();
            CreateMap<Order, OrderWithDetailsDto>();
            CreateMap<CreateOrderDto, Order>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Customer, opt => opt.Ignore())
                .ForMember(dest => dest.Provider, opt => opt.Ignore());

            CreateMap<Order, OrderListDto>()
                .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => $"DEL{src.Id.ToString().Substring(0, 3).ToUpper()}"))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Profile.FullName))
                .ForMember(dest => dest.CustomerEmail, opt => opt.MapFrom(src => src.Customer.Email))
                .ForMember(dest => dest.Item, opt => opt.MapFrom(src => src.Items.FirstOrDefault()))

                .ForMember(dest => dest.DeliveryAddress, opt => opt.MapFrom(src => src.Customer.Profile.Address))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Customer.Profile.Phone))
                .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src => src.RentalStart ?? DateTime.MinValue))
                .ForMember(dest => dest.DeliveredDate, opt => opt.MapFrom(src => src.Status == OrderStatus.in_use ? src.UpdatedAt : (DateTime?)null))
                .ForMember(dest => dest.ReturnDate, opt => opt.MapFrom(src => src.RentalEnd));

            CreateMap<OrderItem, OrderItemListDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ProductSize, opt => opt.MapFrom(src => src.Product.Size))
                .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl))
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays));
        }
    }
}
