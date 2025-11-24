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
        /// Chờ Admin xem xét và đưa ra quyết định cuối cùng
        /// Trạng thái này xảy ra khi leo thang tranh chấp lên Admin
        /// </summary>
        PENDING_ADMIN_REVIEW,

        /// <summary>
        /// Đã được Admin giải quyết
        /// </summary>
        RESOLVED_BY_ADMIN,

        /// <summary>
        /// Đã giải quyết xong (đã xử lý tài chính)
        /// Trạng thái này khi hai bên tự thỏa thuận hoặc customer chấp nhận
        /// </summary>
        RESOLVED
    }
}

