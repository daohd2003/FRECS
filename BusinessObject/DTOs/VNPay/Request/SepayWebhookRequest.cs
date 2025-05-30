using System.Text.Json.Serialization;

namespace BusinessObject.DTOs.VNPay.Request
{
    public class SepayWebhookRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; } // ID giao dịch trên SePay

        [JsonPropertyName("gateway")]
        public string Gateway { get; set; } = string.Empty; // Tên ngân hàng

        [JsonPropertyName("transactionDate")]
        public string TransactionDate { get; set; } = string.Empty; // Thời gian giao dịch

        [JsonPropertyName("accountNumber")]
        public string BankAccount { get; set; } = string.Empty; // Số tài khoản ngân hàng nhận

        [JsonPropertyName("code")]
        public string? Code { get; set; } // Mã code thanh toán, có thể null

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty; // Nội dung chuyển khoản

        [JsonPropertyName("transferType")]
        public string TransferType { get; set; } = string.Empty; // Loại giao dịch ("in" hoặc "out")

        [JsonPropertyName("transferAmount")]
        public decimal Amount { get; set; } // Số tiền giao dịch

        [JsonPropertyName("accumulated")]
        public decimal Accumulated { get; set; } // Số dư tài khoản (lũy kế)

        [JsonPropertyName("subAccount")]
        public string? SubAccount { get; set; } // Tài khoản phụ, có thể null

        [JsonPropertyName("referenceCode")]
        public string ReferenceCode { get; set; } = string.Empty; // Mã tham chiếu SMS

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty; // Toàn bộ nội dung tin nhắn SMS

        // Trường tự tính, không map từ JSON, dùng để lưu mã giao dịch trích từ content
        [JsonIgnore]
        public string TransactionCode { get; set; } = string.Empty;

        // Kiểm tra giao dịch thành công dựa vào transferType và số tiền
        [JsonIgnore]
        public bool IsSuccess => TransferType == "in" && Amount > 0;
    }
}
