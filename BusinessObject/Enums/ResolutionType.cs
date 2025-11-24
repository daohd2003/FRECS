namespace BusinessObject.Enums
{
    /// <summary>
    /// Loại quyết định của Admin
    /// </summary>
    public enum ResolutionType
    {
        /// <summary>
        /// Giữ nguyên yêu cầu - Phán quyết theo hướng Provider
        /// </summary>
        UPHOLD_CLAIM,

        /// <summary>
        /// Hủy bỏ yêu cầu - Phán quyết theo hướng Customer
        /// </summary>
        REJECT_CLAIM,

        /// <summary>
        /// Thỏa hiệp - Điều chỉnh mức bồi thường
        /// </summary>
        COMPROMISE
    }
}

