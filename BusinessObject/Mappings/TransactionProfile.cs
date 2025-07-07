using AutoMapper;
using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.Mappings
{
    public class TransactionProfile : AutoMapper.Profile
    {
        public TransactionProfile()
        {
            CreateMap<Transaction, TransactionSummaryDto>()
                .ForMember(
                    dest => dest.Orders,
                    opt => opt.MapFrom(src => src.Orders.Select(o => new OrderProviderPairDto
                    {
                        OrderId = o.Id,
                        ProviderId = o.ProviderId,
                        OrderAmount = o.TotalAmount
                    }).ToList())
                );
        }
    }
}
