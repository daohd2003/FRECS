using AutoMapper;
using BusinessObject.DTOs.RevenueDtos;
using BusinessObject.Models;
using Profile = AutoMapper.Profile;

namespace BusinessObject.Mappings
{
    public class RevenueProfile : Profile
    {
        public RevenueProfile()
        {
            CreateMap<BankAccount, BankAccountDto>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
            CreateMap<CreateBankAccountDto, BankAccount>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore());
        }
    }
}
