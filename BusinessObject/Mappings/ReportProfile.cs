using BusinessObject.DTOs.ReportDto;
using BusinessObject.Models;

namespace BusinessObject.Mappings
{
    public class ReportProfile : AutoMapper.Profile
    {
        public ReportProfile()
        {
            CreateMap<Report, ReportDTO>().ReverseMap();
            CreateMap<Report, ReportDTO>();
        }
    }
}
