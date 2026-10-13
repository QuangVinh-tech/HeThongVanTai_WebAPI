using System.Globalization;

namespace HeThongVanTai.Web.Infrastructure;

/// <summary>
/// Tài khoản nhận tiền chuyển khoản (mục "BankTransfer" trong appsettings.json của Web).
/// Ảnh QR được tạo theo chuẩn VietQR (img.vietqr.io), điền sẵn số tiền và nội dung là mã đặt vé.
/// </summary>
public class BankQrOptions
{
    /// <summary>Mã hoặc BIN ngân hàng theo VietQR, ví dụ MB, VCB, TCB, ACB, BIDV, 970422.</summary>
    public string BankId { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    /// <summary>Kiểu ảnh: compact2 (có logo, thông tin), compact, qr_only, print.</summary>
    public string Template { get; set; } = "compact2";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BankId) && !string.IsNullOrWhiteSpace(AccountNo);

    public string BuildImageUrl(decimal amount, string content)
    {
        var money = ((long)decimal.Round(amount)).ToString(CultureInfo.InvariantCulture);
        var url = "https://img.vietqr.io/image/"
                  + Uri.EscapeDataString(BankId.Trim()) + "-"
                  + Uri.EscapeDataString(AccountNo.Trim()) + "-"
                  + Uri.EscapeDataString(string.IsNullOrWhiteSpace(Template) ? "compact2" : Template.Trim()) + ".png"
                  + "?amount=" + money
                  + "&addInfo=" + Uri.EscapeDataString(content);
        if (!string.IsNullOrWhiteSpace(AccountName))
            url += "&accountName=" + Uri.EscapeDataString(AccountName.Trim());
        return url;
    }
}
