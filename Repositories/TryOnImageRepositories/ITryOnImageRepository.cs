using BusinessObject.Models;
using Repositories.RepositoryBase;

namespace Repositories.TryOnImageRepositories
{
    public interface ITryOnImageRepository : IRepository<TryOnImage>
    {
        /// <summary>
        /// Lấy tất cả ảnh Try-On của một customer
        /// </summary>
        Task<IEnumerable<TryOnImage>> GetByCustomerIdAsync(Guid customerId, int pageNumber = 1, int pageSize = 20);

        /// <summary>
        /// Đếm số ảnh Try-On của một customer
        /// </summary>
        Task<int> CountByCustomerIdAsync(Guid customerId);

        /// <summary>
        /// Lấy các ảnh đã hết hạn và chưa bị xóa
        /// </summary>
        Task<IEnumerable<TryOnImage>> GetExpiredImagesAsync(int batchSize = 100);

        /// <summary>
        /// Đánh dấu ảnh đã bị xóa
        /// </summary>
        Task MarkAsDeletedAsync(Guid id);

        /// <summary>
        /// Đánh dấu nhiều ảnh đã bị xóa
        /// </summary>
        Task MarkAsDeletedBatchAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Xóa vĩnh viễn các record đã đánh dấu xóa (cleanup)
        /// </summary>
        Task<int> PurgeDeletedRecordsAsync(int olderThanDays = 30);

        /// <summary>
        /// Hard delete - xóa hoàn toàn nhiều record khỏi database
        /// </summary>
        Task HardDeleteBatchAsync(IEnumerable<Guid> ids);
    }
}
