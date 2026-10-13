namespace HeThongVanTai.Models.DTO;

/// <summary>Dữ liệu SePay gửi tới webhook khi có biến động số dư (chỉ khai báo các trường cần dùng).</summary>
public class SePayWebhookDto
{
    public long Id { get; set; }                       // mã giao dịch phía SePay
    public string? Gateway { get; set; }               // tên ngân hàng
    public string? AccountNumber { get; set; }         // số tài khoản nhận
    public string? Code { get; set; }                  // mã thanh toán SePay tự nhận diện (nếu có)
    public string? Content { get; set; }               // nội dung chuyển khoản
    public string? TransferType { get; set; }          // "in" = tiền vào, "out" = tiền ra
    public decimal TransferAmount { get; set; }        // số tiền
    public string? ReferenceCode { get; set; }         // mã tham chiếu của ngân hàng
}
