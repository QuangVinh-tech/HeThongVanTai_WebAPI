namespace HeThongVanTai.Web.Infrastructure;

/// <summary>
/// Phiên bản và ngày hiệu lực của Điều khoản sử dụng, Chính sách bảo mật.
/// Khi sửa nội dung hai trang này: tăng TermsVersion và đổi EffectiveDate;
/// phiên bản được gửi kèm khi đăng ký và lưu cùng ngày đồng ý.
/// </summary>
public static class LegalInfo
{
    public const string TermsVersion = "1.0";
    public static readonly DateTime EffectiveDate = new(2026, 10, 18);
}
