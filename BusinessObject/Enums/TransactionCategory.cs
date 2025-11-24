namespace BusinessObject.Enums
{
    /// <summary>
    /// Phân loại giao dịch để phân biệt các loại giao dịch khác nhau
    /// TÁCH BIỆT với TransactionStatus để không ảnh hưởng đến logic hiện tại
    /// </summary>
    public enum TransactionCategory
    {
        /// <summary>
        /// Giao dịch mua hàng từ customer
        /// </summary>
        Purchase,

        /// <summary>
        /// Giao dịch thuê từ customer
        /// </summary>
        Rental,

        /// <summary>
        /// Hoàn tiền cọc cho customer sau khi trả đồ
        /// </summary>
        DepositRefund,

        /// <summary>
        /// Chuyển tiền cho provider từ yêu cầu rút tiền
        /// </summary>
        ProviderWithdrawal,

        /// <summary>
        /// Tiền phạt từ customer (vi phạm thuê)
        /// </summary>
        Penalty,

        /// <summary>
        /// Tiền bồi thường cho provider
        /// </summary>
        Compensation
    }
}
