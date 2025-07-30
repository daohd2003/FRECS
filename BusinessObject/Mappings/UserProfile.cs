using BusinessObject.DTOs.ReportDto;
using BusinessObject.DTOs.UsersDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class UserProfile : AutoMapper.Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDto>();

            CreateMap<User, AdminViewModel>()
                // Chỉ định rằng thuộc tính FullName của AdminViewModel sẽ được lấy từ User.Profile.FullName
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Profile.FullName))
                // Tạm thời bỏ qua ActiveTaskCount để giải quyết lỗi trước mắt.
                // Logic đếm số task sẽ được thêm vào sau.
                .ForMember(dest => dest.ActiveTaskCount, opt => opt.Ignore());
        }
    }
}
