namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái của một vật phẩm hoặc sản phẩm trong kho.
    /// </summary>
    public enum AvailabilityStatus
    {
        available,
        pending,
        rejected,// Có sẵn, có thể sử dụng hoặc đặt hàng
        unavailable, // Không có sẵn, hết hàng hoặc tạm thời không sử dụng được
        archived,    // Lưu trữ (không thể thuê, nhưng giữ lại cho lịch sử)
        deleted      // Đã xóa (ẩn khỏi UI)
    }
}
