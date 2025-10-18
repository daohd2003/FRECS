using BusinessObject.DTOs.DepositDto;

namespace Services.DepositServices
{
    public interface IDepositService
    {
        Task<DepositStatsDto> GetDepositStatsAsync(Guid customerId);
        Task<List<DepositHistoryDto>> GetDepositHistoryAsync(Guid customerId);
    }
}

