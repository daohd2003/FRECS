using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO cho việc tạo nhiều vi phạm cùng lúc (nhiều sản phẩm trong 1 đơn hàng)
    /// KHÔNG CẦN cung cấp ViolationId - Hệ thống sẽ tự động generate
    /// </summary>
    public class CreateMultipleViolationsRequestDto
    {
        /// <summary>
        /// ID của đơn hàng cần tạo vi phạm
        /// </summary>
        [Required(ErrorMessage = "OrderId là bắt buộc")]
        public Guid OrderId { get; set; }

        /// <summary>
        /// Danh sách vi phạm cho từng sản phẩm trong đơn hàng
        /// Mỗi vi phạm sẽ được tự động tạo ViolationId
        /// </summary>
        [Required(ErrorMessage = "Danh sách vi phạm là bắt buộc")]
        [MinLength(1, ErrorMessage = "Cần ít nhất 1 vi phạm")]
        public List<CreateRentalViolationDto> Violations { get; set; }
    }
}


