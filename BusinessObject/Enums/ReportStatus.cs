namespace BusinessObject.Enums
{
    /// <summary>
    /// Trạng thái của một báo cáo (report).
    /// </summary>
    public enum ReportStatus
    {
        open,           // Báo cáo mới, chưa xử lý
        in_progress,    // Đang trong quá trình xử lý
        resolved,        // Báo cáo đã được giải quyết xong
        awaiting_customer_response,
        ResolutionNote
    }
}
