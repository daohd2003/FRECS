using BusinessObject.DTOs.ReportDto;
using BusinessObject.Models;
using BusinessObject.Utilities;
using System.Text.Json;

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
                .ForMember(dest => dest.OrderItemId, opt => opt.MapFrom(src => src.OrderItemId))
                .ForMember(dest => dest.ReportType, opt => opt.MapFrom(src => src.ReportType))
                .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => 
                    src.Order != null ? $"ORD-{src.Order.Id.ToString().Substring(0, 8).ToUpper()}" : null))
                .ForMember(dest => dest.EvidenceImages, opt => opt.MapFrom(src => 
                    !string.IsNullOrEmpty(src.EvidenceImages) 
                        ? JsonSerializer.Deserialize<List<string>>(src.EvidenceImages, (JsonSerializerOptions)null) 
                        : null))
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
                            Price = oi.DailyRate,
                            TransactionType = oi.TransactionType,
                            RentalDays = oi.RentalDays,
                            DepositAmount = oi.DepositPerUnit
                        }).ToList()
                        : null))
                .ForMember(dest => dest.ReportedProduct, opt => opt.MapFrom(src => 
                    src.OrderItem != null && src.OrderItem.Product != null 
                        ? new OrderProductInfo
                        {
                            ProductId = src.OrderItem.ProductId,
                            ProductName = src.OrderItem.Product.Name,
                            PrimaryImageUrl = src.OrderItem.Product.Images != null && src.OrderItem.Product.Images.Any()
                                ? (src.OrderItem.Product.Images.FirstOrDefault(img => img.IsPrimary) != null 
                                    ? src.OrderItem.Product.Images.FirstOrDefault(img => img.IsPrimary).ImageUrl
                                    : src.OrderItem.Product.Images.FirstOrDefault() != null 
                                        ? src.OrderItem.Product.Images.FirstOrDefault().ImageUrl 
                                        : null)
                                : null,
                            Quantity = src.OrderItem.Quantity,
                            Price = src.OrderItem.DailyRate,
                            TransactionType = src.OrderItem.TransactionType,
                            RentalDays = src.OrderItem.RentalDays,
                            DepositAmount = src.OrderItem.DepositPerUnit
                        }
                        : null));

            CreateMap<ReportDTO, Report>()
                .ForMember(dest => dest.EvidenceImages, opt => opt.MapFrom(src => 
                    src.EvidenceImages != null && src.EvidenceImages.Any() 
                        ? JsonSerializer.Serialize(src.EvidenceImages, (JsonSerializerOptions)null) 
                        : null))
                .ReverseMap()
                .ForMember(dest => dest.EvidenceImages, opt => opt.MapFrom(src => 
                    !string.IsNullOrEmpty(src.EvidenceImages) 
                        ? JsonSerializer.Deserialize<List<string>>(src.EvidenceImages, (JsonSerializerOptions)null) 
                        : null));
        }
    }
}
