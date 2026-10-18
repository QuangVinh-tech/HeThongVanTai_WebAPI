using System.Net;

namespace HeThongVanTai.Services.Email;

/// <summary>
/// Khung HTML dùng chung cho mọi email hệ thống gửi (đặt lại mật khẩu và các thông báo sau này),
/// để email có cùng giao diện. Văn bản truyền vào phải đã được HTML-encode nếu chứa dữ liệu người dùng.
/// </summary>
public static class EmailTemplates
{
    private const string Brand = "VậnTải HK";

    /// <summary>Bọc nội dung HTML vào khung chung (tiêu đề, thân, chân thư).</summary>
    public static string Layout(string heading, string bodyHtml) =>
        "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#2b2b2b\">" +
        $"<div style=\"background:#9b2c54;color:#fff;padding:16px 24px;font-size:18px;font-weight:bold\">{Brand}</div>" +
        "<div style=\"padding:24px;border:1px solid #eadbe1;border-top:0\">" +
        $"<h2 style=\"margin:0 0 12px;font-size:20px\">{WebUtility.HtmlEncode(heading)}</h2>" +
        bodyHtml +
        "</div>" +
        $"<div style=\"padding:12px 24px;font-size:12px;color:#888\">Email tự động từ {Brand}. Vui lòng không trả lời email này.</div>" +
        "</div>";

    /// <summary>Email chứa liên kết đặt lại mật khẩu, hết hạn sau số phút cho trước.</summary>
    public static EmailMessage ResetPassword(string to, string link, int minutes)
    {
        var href = WebUtility.HtmlEncode(link);
        var body =
            "<p>Xin chào,</p>" +
            "<p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>" +
            $"<p style=\"margin:24px 0\"><a href=\"{href}\" style=\"background:#9b2c54;color:#fff;padding:12px 24px;" +
            "border-radius:6px;text-decoration:none;font-weight:bold\">Đặt lại mật khẩu</a></p>" +
            $"<p>Liên kết có hiệu lực trong <strong>{minutes} phút</strong> và chỉ dùng được một lần.</p>" +
            $"<p style=\"font-size:13px;color:#666\">Nếu nút không bấm được, hãy dán liên kết này vào trình duyệt:<br>{href}</p>" +
            "<p>Nếu bạn không yêu cầu, hãy bỏ qua email này, mật khẩu của bạn vẫn giữ nguyên.</p>";

        return new EmailMessage
        {
            To = to,
            Subject = "Đặt lại mật khẩu VậnTải HK",
            Html = Layout("Đặt lại mật khẩu", body),
            // Bản chữ thường: ở Development được ghi vào log khi chưa cấu hình SMTP, để thử không cần gửi mail thật
            Text = $"Dat lai mat khau (hieu luc {minutes} phut, dung mot lan): {link}"
        };
    }
}
