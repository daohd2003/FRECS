using BusinessObject.Enums;

namespace BusinessObject.DTOs.ReportDto
{
    public class ReportDTO
    {
        public Guid Id { get; set; }  // ID duy nhất cho mỗi báo cáo

        public Guid ReporterId { get; set; }  // Người thực hiện báo cáo

        public Guid? ReporteeId { get; set; } = null; // Người bị báo cáo

        public Guid? OrderId { get; set; }  // ID đơn hàng (nếu là report liên quan đến order)

        public ReportType ReportType { get; set; } = ReportType.General;  // Loại báo cáo

        public string Subject { get; set; }  // Chủ đề/nguyên nhân báo cáo (Ví dụ: Spam, Lừa đảo)

        public string? Description { get; set; }  // Mô tả chi tiết (có thể null)

        public ReportStatus? Status { get; set; }  // Trạng thái xử lý (Pending, Reviewed, Rejected...)

        public DateTime? CreatedAt { get; set; }

        public ReportPriority Priority { get; set; }
    }
}
