using AutoMapper;
using BusinessObject.DTOs.RentalViolationDto;
using BusinessObject.Models;

namespace BusinessObject.Mappings
{
    public class RentalViolationProfile : AutoMapper.Profile
    {
        public RentalViolationProfile()
        {
            // RentalViolation -> RentalViolationDto
            CreateMap<RentalViolation, RentalViolationDto>()
                .ForMember(dest => dest.EvidenceCount,
                    opt => opt.MapFrom(src => src.Images.Count));

            // RentalViolationImage -> RentalViolationImageDto
            CreateMap<RentalViolationImage, RentalViolationImageDto>()
                .ForMember(dest => dest.UploadedByDisplay,
                    opt => opt.MapFrom(src => src.UploadedBy == Enums.EvidenceUploadedBy.PROVIDER 
                        ? "Nhà cung cấp" 
                        : "Khách hàng"));
        }
    }
}