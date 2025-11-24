using AutoMapper;
using BusinessObject.DTOs.IssueResolutionDto;
using BusinessObject.Models;

namespace BusinessObject.Mappings
{
    public class IssueResolutionProfile : AutoMapper.Profile
    {
        public IssueResolutionProfile()
        {
            CreateMap<CreateIssueResolutionDto, IssueResolution>();

            CreateMap<IssueResolution, IssueResolutionResponseDto>()
                .ForMember(dest => dest.ProcessedByAdminName,
                    opt => opt.MapFrom(src => src.ProcessedByAdmin.Profile != null 
                        ? src.ProcessedByAdmin.Profile.FullName 
                        : src.ProcessedByAdmin.Email));
        }
    }
}

