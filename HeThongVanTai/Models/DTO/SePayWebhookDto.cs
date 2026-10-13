
using System.Text.Json.Serialization;

namespace HeThongVanTai.Models.DTO;

/// <summary>
/// Dữ liệu SePay gửi tới webhook khi tài khoản có biến động số dư.
/// </summary>
public class SePayWebhookDto
{
    /// <summary>Mã giao dịch duy nhất do SePay cấp.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Tên ngân hàng, ví dụ MB.</summary>
    [JsonPropertyName("gateway")]
    public string? Gateway { get; set; }

    /// <summary>Số tài khoản nhận tiền.</summary>
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    /// <summary>Mã thanh toán SePay nhận diện được, nếu có.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>Nội dung chuyển khoản.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>Loại giao dịch: in là tiền vào, out là tiền ra.</summary>
    [JsonPropertyName("transferType")]
    public string? TransferType { get; set; }

    /// <summary>Số tiền giao dịch, đơn vị VND.</summary>
    [JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; set; }

    /// <summary>Mã tham chiếu giao dịch ngân hàng.</summary>
    [JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; set; }
}
