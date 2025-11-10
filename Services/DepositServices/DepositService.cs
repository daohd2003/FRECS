using BusinessObject.DTOs.DepositDto;
using BusinessObject.Enums;
using Repositories.DepositRepositories;

namespace Services.DepositServices
{
    public class DepositService : IDepositService
    {
        private readonly IDepositRepository _depositRepo;

        public DepositService(IDepositRepository depositRepo)
        {
            _depositRepo = depositRepo;
        }

        public async Task<DepositStatsDto> GetDepositStatsAsync(Guid customerId)
        {
            // Get all customer orders with transactions
            var customerOrders = await _depositRepo.GetCustomerOrdersWithDepositsAsync(customerId);

            // Deposits Refunded: Not implemented yet - requires admin deposit refund system
            // Transaction completed means payment for ORDER, not deposit refund
            var depositsRefunded = 0m;

            // Pending Refunds: Not implemented yet - requires admin deposit refund system
            // Transaction status is for ORDER payment, not deposit refund
            var pendingRefunds = 0m;

            // Refund Issues: Not implemented yet - requires admin deposit refund system  
            // Transaction status is for ORDER payment, not deposit refund
            var refundIssues = 0;

            return new DepositStatsDto
            {
                DepositsRefunded = depositsRefunded,
                PendingRefunds = pendingRefunds,
                RefundIssues = refundIssues
            };
        }

        public async Task<List<DepositHistoryDto>> GetDepositHistoryAsync(Guid customerId)
        {
            // Deposit history not implemented yet - requires admin deposit refund system
            // Transaction status is for ORDER payment, not deposit refund
            // Return empty list until deposit refund system is implemented
            return new List<DepositHistoryDto>();
        }
    }
}

