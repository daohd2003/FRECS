namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái của biên bản vi phạm
    /// </summary>
    public enum ViolationStatus
    {
        /// <summary>
        /// Chờ Customer phản hồi
        /// </summary>
        PENDING,

        /// <summary>
        /// Customer đồng ý với khoản phạt
        /// </summary>
        CUSTOMER_ACCEPTED,

        /// <summary>
        /// Customer từ chối, yêu cầu thương lượng
        /// </summary>
        CUSTOMER_REJECTED,

        /// <summary>
        /// Đã giải quyết xong (đã xử lý tài chính)
        /// </summary>
        RESOLVED
    }
}

