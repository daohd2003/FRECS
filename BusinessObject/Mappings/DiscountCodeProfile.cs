using AutoMapper;
using BusinessObject.DTOs.DiscountCodeDto;
using BusinessObject.Models;
using AutoMapperProfile = AutoMapper.Profile;

namespace BusinessObject.Mappings
{
    public class DiscountCodeProfile : AutoMapperProfile
    {
        public DiscountCodeProfile()
        {
            CreateMap<DiscountCode, DiscountCodeDto>().ReverseMap();
            CreateMap<CreateDiscountCodeDto, DiscountCode>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Active"))
                .ForMember(dest => dest.UsedCount, opt => opt.MapFrom(src => 0));

            CreateMap<UpdateDiscountCodeDto, DiscountCode>()
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UsedCount, opt => opt.Ignore());

            CreateMap<UsedDiscountCode, UsedDiscountCodeDto>()
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Profile != null ? src.User.Profile.FullName : ""))
                .ForMember(dest => dest.DiscountCode, opt => opt.MapFrom(src => src.DiscountCode.Code));
        }
    }
}
