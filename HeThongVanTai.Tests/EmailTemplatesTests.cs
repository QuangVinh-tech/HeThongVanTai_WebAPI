using HeThongVanTai.Services.Email;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Mẫu email dùng chung: email đặt lại mật khẩu.</summary>
public class EmailTemplatesTests
{
    private const string Link = "https://localhost:7300/Account/ResetPassword?email=a%40b.com&token=abc-_123";

    [Fact]
    public void Email_dat_lai_mat_khau_chua_lien_ket_va_thoi_han()
    {
        var m = EmailTemplates.ResetPassword("a@b.com", Link, 30);

        Assert.Equal("a@b.com", m.To);
        Assert.Contains("30 phút", m.Html);
        Assert.Contains("Đặt lại mật khẩu", m.Subject);
        // Trong HTML, dấu & của liên kết phải được mã hóa
        Assert.Contains("email=a%40b.com&amp;token=abc-_123", m.Html);
        // Bản chữ thường giữ nguyên liên kết để dán được
        Assert.Contains(Link, m.Text);
    }

    [Fact]
    public void Khung_chung_ma_hoa_tieu_de()
    {
        var html = EmailTemplates.Layout("<b>x</b>", "<p>noi dung</p>");

        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.Contains("<p>noi dung</p>", html);
        Assert.Contains("VậnTải HK", html);
    }
}
