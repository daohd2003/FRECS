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
                .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType))
                // Map PricePerUnit based on TransactionType
                .ForMember(dest => dest.PricePerUnit, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.purchase 
                        ? src.Product.PurchasePrice 
                        : src.Product.PricePerDay))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays))
                // Calculate TotalItemPrice based on TransactionType
                .ForMember(dest => dest.TotalItemPrice, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.purchase 
                        ? src.Product.PurchasePrice * src.Quantity
                        : src.Product.PricePerDay * src.Quantity * (src.RentalDays ?? 1)))
                // Map DepositPerUnit based on TransactionType
                .ForMember(dest => dest.DepositPerUnit, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.rental 
                        ? src.Product.SecurityDeposit 
                        : 0m))
                // Calculate TotalDepositAmount for rental items only
                .ForMember(dest => dest.TotalDepositAmount, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.rental 
                        ? src.Product.SecurityDeposit * src.Quantity
                        : 0m))
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
                .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
                .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl))
                .ForMember(dest => dest.AvailableRentalStock, opt => opt.MapFrom(src => src.Product.RentalQuantity))
                .ForMember(dest => dest.AvailablePurchaseStock, opt => opt.MapFrom(src => src.Product.PurchaseQuantity));


            CreateMap<CartAddRequestDto, CartItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Id sẽ được tạo tự động khi thêm vào DB
                .ForMember(dest => dest.CartId, opt => opt.Ignore()) // CartId sẽ được gán thủ công trong Service
                .ForMember(dest => dest.Cart, opt => opt.Ignore()) // Cart navigation property
                .ForMember(dest => dest.Product, opt => opt.Ignore()) // Product navigation property sẽ được EF Core quản lý
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId)) // Map ProductId
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity)) // Map Quantity
                .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType)) // Map TransactionType
                // Only map RentalDays for Rental transactions
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.rental ? src.RentalDays : null))
                // Only map StartDate for Rental transactions  
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => 
                    src.TransactionType == BusinessObject.Enums.TransactionType.rental ? src.StartDate : null))
                // EndDate will be calculated in service for Rental transactions
                .ForMember(dest => dest.EndDate, opt => opt.Ignore());

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
