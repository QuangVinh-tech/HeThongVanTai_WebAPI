using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain;

/// <summary>
/// Bằng chứng đồng ý Điều khoản sử dụng và Chính sách bảo mật khi đăng ký
/// (Nghị định 13/2023/NĐ-CP): ai đồng ý, phiên bản nào, lúc nào (giờ UTC).
/// Lưu ở CSDL tài khoản, bảng UserConsents.
/// </summary>
public class UserConsent
{
    [Key] public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";

    [Required, MaxLength(20)]
    public string TermsVersion { get; set; } = "";

    public DateTime AcceptedAt { get; set; }
}
