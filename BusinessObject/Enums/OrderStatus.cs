namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái của một đơn hàng.
    /// </summary>
    public enum OrderStatus
    {
        pending,    // Đang chờ xử lý
        approved,   // Đã duyệt, chuẩn bị hoặc đang thực hiện
        in_transit, // Đang vận chuyển
        in_use,     // Đang được sử dụng (áp dụng cho thuê, mượn)
        returned,   // Đã trả lại hoặc hoàn trả
        cancelled,   // Đã hủy bỏ đơn hàng
        returned_with_issue
    }
}
