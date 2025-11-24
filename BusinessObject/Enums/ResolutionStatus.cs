namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái giải quyết của tranh chấp
    /// </summary>
    public enum ResolutionStatus
    {
        /// <summary>
        /// Chờ xử lý
        /// </summary>
        PENDING,

        /// <summary>
        /// Đang xem xét
        /// </summary>
        UNDER_REVIEW,

        /// <summary>
        /// Đã hoàn thành
        /// </summary>
        COMPLETED
    }
}

