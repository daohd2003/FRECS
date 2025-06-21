using BusinessObject.Enums;
using BusinessObject.Models;
using Repositories.RepositoryBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.FeedbackRepositories
{
    public interface IFeedbackRepository : IRepository<Feedback>
    {
        Task<IEnumerable<Feedback>> GetFeedbacksByTargetAsync(FeedbackTargetType targetType, Guid targetId);
        Task<IEnumerable<Feedback>> GetFeedbacksByCustomerIdAsync(Guid customerId);
        Task<IEnumerable<Feedback>> GetFeedbacksByProviderIdAsync(Guid providerId);
        Task<bool> HasUserFeedbackedOrderItemAsync(Guid customerId, Guid orderItemId);
        Task<bool> HasUserFeedbackedOrderAsync(Guid customerId, Guid orderId);
    }
}
