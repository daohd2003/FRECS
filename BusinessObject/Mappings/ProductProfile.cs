using AutoMapper;
using BusinessObject.DTOs.ProductDto;
using BusinessObject.Enums;
using BusinessObject.Mappings.Helpers;
using BusinessObject.Models;
using System.Linq;

namespace BusinessObject.Mappings
{
    public class ProductProfile : AutoMapper.Profile
    {
        public ProductProfile()
        {
            CreateMap<Product, ProductDTO>()
                .ForMember(dest => dest.ProviderName, opt => opt.MapFrom(src => src.Provider.Profile.FullName))
                .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src => src.AvailabilityStatus.ToString()))
                .ForMember(dest => dest.PrimaryImagesUrl, opt => opt.MapFrom(src =>
                    src.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images));

            CreateMap<ProductDTO, Product>()
                .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src =>
                    EnumHelper.ParseOrDefault<AvailabilityStatus>(src.AvailabilityStatus, AvailabilityStatus.available)))
                .ForMember(dest => dest.Provider, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<ProductImage, ProductImageDTO>().ReverseMap();
        }
    }
}