using BusinessObject.DTOs.FeedbackDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class FeedbackProfile : AutoMapper.Profile
    {
        public FeedbackProfile()
        {
            CreateMap<FeedbackRequestDto, Feedback>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.TargetType == FeedbackTargetType.Product ? src.TargetId : (Guid?)null))
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.TargetType == FeedbackTargetType.Order ? src.TargetId : (Guid?)null))
                .ForMember(dest => dest.OrderItemId, opt => opt.MapFrom(src => src.TargetType == FeedbackTargetType.Product ? src.OrderItemId : (Guid?)null))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Ánh xạ Feedback Entity sang FeedbackResponseDto (cho Read)
            CreateMap<Feedback, FeedbackResponseDto>()
                .ForMember(dest => dest.FeedbackId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TargetId, opt => opt.MapFrom(src =>
                    src.TargetType == FeedbackTargetType.Product && src.ProductId.HasValue ? src.ProductId.Value :
                    (src.TargetType == FeedbackTargetType.Order && src.OrderId.HasValue ? src.OrderId.Value : Guid.Empty)))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null && src.Customer.Profile != null ? src.Customer.Profile.FullName : null))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.OrderIdFromFeedback, opt => opt.MapFrom(src => src.Order != null ? src.Order.Id.ToString() : null))
                .ForMember(dest => dest.SubmittedAt, opt => opt.MapFrom(src => src.CreatedAt))

                .ForMember(dest => dest.ProviderResponse, opt => opt.MapFrom(src => src.ProviderResponse))
                .ForMember(dest => dest.ProviderResponseAt, opt => opt.MapFrom(src => src.ProviderResponseAt))
                .ForMember(dest => dest.ProviderResponseById, opt => opt.MapFrom(src => src.ProviderResponseById))
                .ForMember(dest => dest.ProviderResponderName, opt => opt.MapFrom(src => src.ProviderResponder != null && src.ProviderResponder.Profile != null ? src.ProviderResponder.Profile.FullName : null));

            // Ánh xạ FeedbackRequestDto sang Feedback Entity (cho Update)
            CreateMap<FeedbackRequestDto, Feedback>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerId, opt => opt.Ignore())
                .ForMember(dest => dest.TargetType, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.OrderId, opt => opt.Ignore())
                .ForMember(dest => dest.OrderItemId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        }
    }
}
