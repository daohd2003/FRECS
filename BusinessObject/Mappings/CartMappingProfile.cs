using BusinessObject.DTOs.CartDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class CartMappingProfile : AutoMapper.Profile
    {
        public CartMappingProfile()
        {
            // Map Cart to CartDto
            CreateMap<Cart, CartDto>()
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()); // Sửa từ GrandTotal thành TotalAmount

            CreateMap<CartItem, CartItemDto>()
                .ForMember(dest => dest.ItemId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ProductSize, opt => opt.MapFrom(src => src.Product.Size))
                .ForMember(dest => dest.PricePerUnit, opt => opt.MapFrom(src => src.Product.PricePerDay))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays))
                .ForMember(dest => dest.TotalItemPrice, opt => opt.MapFrom(src => src.Product.PricePerDay * src.Quantity * src.RentalDays))
                .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl));


            CreateMap<CartAddRequestDto, CartItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Id sẽ được tạo tự động khi thêm vào DB
                .ForMember(dest => dest.CartId, opt => opt.Ignore()) // CartId sẽ được gán thủ công trong Service
                .ForMember(dest => dest.Product, opt => opt.Ignore()) // Product navigation property sẽ được EF Core quản lý
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId)) // Map ProductId
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity)) // Map Quantity
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays ?? 1)) // Default handled in service as well
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate ?? DateTime.UtcNow.Date.AddDays(1))); // Default handled in service as well

            CreateMap<CartUpdateRequestDto, CartItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Id của CartItem hiện tại không thay đổi
                .ForMember(dest => dest.CartId, opt => opt.Ignore()) // CartId của CartItem hiện tại không thay đổi
                .ForMember(dest => dest.Product, opt => opt.Ignore()) // Product navigation property không thay đổi
                .ForMember(dest => dest.ProductId, opt => opt.Ignore()) // ProductId không thay đổi
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity)) // Map Quantity
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays)); // Map RentalDays
        }
    }
}
