namespace BusinessObject.Enums
{
    /// <summary>
    /// Loại vi phạm trong đơn hàng cho thuê
    /// </summary>
    public enum ViolationType
    {
        /// <summary>
        /// Sản phẩm bị hư hỏng
        /// </summary>
        DAMAGED,

        /// <summary>
        /// Trả trễ hạn
        /// </summary>
        LATE_RETURN,

        /// <summary>
        /// Không trả lại sản phẩm
        /// </summary>
        NOT_RETURNED
    }
}

