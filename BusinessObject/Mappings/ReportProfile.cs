using BusinessObject.DTOs.ReportDto;
using BusinessObject.Models;
using BusinessObject.Utilities;

namespace BusinessObject.Mappings
{
    public class ReportProfile : AutoMapper.Profile
    {
        public ReportProfile()
        {
            CreateMap<Report, ReportViewModel>()
                .ForMember(dest => dest.ReporterId, opt => opt.MapFrom(src => src.ReporterId))
                .ForMember(dest => dest.ReporteeId, opt => opt.MapFrom(src => src.ReporteeId))
                .ForMember(dest => dest.ReporterName, opt => opt.MapFrom(src => src.Reporter != null && src.Reporter.Profile != null ? src.Reporter.Profile.FullName : "Unknown"))
                .ForMember(dest => dest.ReporterEmail, opt => opt.MapFrom(src => src.Reporter != null ? src.Reporter.Email : "Unknown"))
                .ForMember(dest => dest.ReporteeName, opt => opt.MapFrom(src => src.Reportee != null && src.Reportee.Profile != null ? src.Reportee.Profile.FullName : "Unknown"))
                .ForMember(dest => dest.ReporteeEmail, opt => opt.MapFrom(src => src.Reportee != null ? src.Reportee.Email : "Unknown"))
                .ForMember(dest => dest.AssignedAdminName, opt => opt.MapFrom(src => src.AssignedAdmin != null && src.AssignedAdmin.Profile != null ? src.AssignedAdmin.Profile.FullName : null))
                .ForMember(dest => dest.DateCreated, opt => opt.MapFrom(src => DateTimeHelper.ToVietnamTime(DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc))))
                // Order-related mappings
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                .ForMember(dest => dest.ReportType, opt => opt.MapFrom(src => src.ReportType))
                .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => 
                    src.Order != null ? $"ORD-{src.Order.Id.ToString().Substring(0, 8).ToUpper()}" : null))
                .ForMember(dest => dest.OrderProducts, opt => opt.MapFrom(src => 
                    src.Order != null && src.Order.Items != null 
                        ? src.Order.Items.Select(oi => new OrderProductInfo
                        {
                            ProductId = oi.ProductId,
                            ProductName = oi.Product != null ? oi.Product.Name : "Unknown",
                            PrimaryImageUrl = oi.Product != null && oi.Product.Images != null && oi.Product.Images.Any()
                                ? (oi.Product.Images.FirstOrDefault(img => img.IsPrimary) != null 
                                    ? oi.Product.Images.FirstOrDefault(img => img.IsPrimary).ImageUrl
                                    : oi.Product.Images.FirstOrDefault() != null 
                                        ? oi.Product.Images.FirstOrDefault().ImageUrl 
                                        : null)
                                : null,
                            Quantity = oi.Quantity,
                            Price = oi.DailyRate
                        }).ToList()
                        : null));

            CreateMap<ReportDTO, Report>().ReverseMap();
        }
    }
}
