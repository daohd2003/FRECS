using BusinessObject.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Repositories.DepositRepositories
{
    public class DepositRepository : IDepositRepository
    {
        private readonly ShareItDbContext _context;

        public DepositRepository(ShareItDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetCustomerOrdersWithDepositsAsync(Guid customerId)
        {
            return await _context.Orders
                .Include(o => o.Transactions)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.UpdatedAt)
                .ToListAsync();
        }
    }
}

