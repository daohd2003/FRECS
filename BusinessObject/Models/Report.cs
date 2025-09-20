using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace BusinessObject.Models
{
    public class Report
    {
        [Key]
        public Guid Id { get; set; }  // ID duy nhất cho mỗi báo cáo

        [Required]
        public Guid ReporterId { get; set; }  // Người thực hiện báo cáo

        public Guid? ReporteeId { get; set; }  // Người bị báo cáo

        [ForeignKey(nameof(ReporterId))]
        public User Reporter { get; set; }

        [ForeignKey(nameof(ReporteeId))]
        public User? Reportee { get; set; }

        [Required]
        [MaxLength(255)]
        public string Subject { get; set; }  // Chủ đề/nguyên nhân báo cáo (Ví dụ: Spam, Lừa đảo)

        public string? Description { get; set; }  // Mô tả chi tiết (có thể null)

        [Required]
        public ReportStatus Status { get; set; }  // Trạng thái xử lý (Pending, Reviewed, Rejected...)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // Thời điểm gửi báo cáo
                                                                    // --- THÊM CÁC THUỘC TÍNH MỚI ---
        public Guid? AssignedAdminId { get; set; } // ID của admin đang xử lý, có thể null

        [ForeignKey(nameof(AssignedAdminId))]
        [JsonIgnore] // Bỏ qua khi serialize để tránh lặp vô hạn
        public User? AssignedAdmin { get; set; }

        public ReportPriority Priority { get; set; } = ReportPriority.Medium; // Thêm độ ưu tiên
        public string? AdminResponse { get; set; } // Phản hồi của admin cho người báo cáo
    }
}
