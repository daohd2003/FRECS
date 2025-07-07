using AutoMapper;
using AutoMapper.QueryableExtensions;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.TransactionRepositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ShareItDbContext _context;
        private readonly IMapper _mapper;

        public TransactionRepository(ShareItDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<TransactionSummaryDto>> GetTransactionsByProviderAsync(Guid providerId)
        {
            return await _context.Transactions
                .Where(t => t.Orders.Any(o => o.ProviderId == providerId) && t.Status == TransactionStatus.completed)
                .OrderByDescending(t => t.TransactionDate)
                .ProjectTo<TransactionSummaryDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalReceivedByProviderAsync(Guid providerId)
        {
            return await _context.Orders
                .Where(o => o.ProviderId == providerId &&
                             o.Transactions.Any(t => t.Status == TransactionStatus.completed))
                .SumAsync(o => o.TotalAmount);
        }
    }
}
