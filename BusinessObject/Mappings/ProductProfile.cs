using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Mappings.Helpers;
using BusinessObject.Models;

namespace BusinessObject.Mappings
{
    public class ProductProfile : AutoMapper.Profile
    {
        public ProductProfile()
        {
            CreateMap<Product, ProductDTO>()
                .ForMember(dest => dest.ProviderName, opt => opt.MapFrom(src => src.Provider.Profile.FullName))
                .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src => src.AvailabilityStatus.ToString()))
                .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender.ToString()))
                .ForMember(dest => dest.RentalStatus, opt => opt.MapFrom(src => src.RentalStatus.ToString()))
                .ForMember(dest => dest.PurchaseStatus, opt => opt.MapFrom(src => src.PurchaseStatus.ToString()))
                .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
                .ForMember(dest => dest.PrimaryImagesUrl, opt => opt.MapFrom(src =>
                    src.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl ?? String.Empty))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images));

            CreateMap<ProductDTO, Product>()
                .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<AvailabilityStatus>(src.AvailabilityStatus, AvailabilityStatus.available)))
                .ForMember(dest => dest.Gender, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<Gender>(src.Gender, Gender.Unisex)))
                .ForMember(dest => dest.RentalStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<RentalStatus>(src.RentalStatus, RentalStatus.Available)))
                .ForMember(dest => dest.PurchaseStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<PurchaseStatus>(src.PurchaseStatus, PurchaseStatus.NotForSale)))
                .ForMember(dest => dest.Provider, opt => opt.Ignore())
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<ProductImage, ProductImageDTO>().ReverseMap();

            CreateMap<ProductRequestDTO, Product>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Gender, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<Gender>(src.Gender, Gender.Unisex)))
                .ForMember(dest => dest.RentalStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<RentalStatus>(src.RentalStatus, RentalStatus.Available)))
                .ForMember(dest => dest.PurchaseStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<PurchaseStatus>(src.PurchaseStatus, PurchaseStatus.NotForSale)))
                .ForMember(dest => dest.Provider, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore())
                .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src => AvailabilityStatus.available))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Feedbacks, opt => opt.Ignore())
                .ForMember(dest => dest.Favorites, opt => opt.Ignore());
        }
    }
}