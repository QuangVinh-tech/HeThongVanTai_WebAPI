using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Web.Models.ViewModels.CaiDat;

public class RefundTierItem
{
    public int HoursBefore { get; set; }
    public int RatePercent { get; set; }
}

/// <summary>Cài đặt công khai do API trả về (cũng là dữ liệu của trang Cài đặt cho Admin).</summary>
public class PublicSettings
{
    public int HoldMinutes { get; set; } = 15;
    public List<RefundTierItem> RefundTiers { get; set; } = new()
    {
        new() { HoursBefore = 24, RatePercent = 100 },
        new() { HoursBefore = 12, RatePercent = 70 },
        new() { HoursBefore = 2, RatePercent = 50 }
    };
    public string? CompanyName { get; set; } = "VậnTải HK";
    public string? LogoUrl { get; set; }
    public string? Hotline { get; set; }
    public string? Email { get; set; }
}

public class TierForm
{
    public int? HoursBefore { get; set; }
    public int? RatePercent { get; set; }
}

public class SettingsForm
{
    [Range(1, 120, ErrorMessage = "Thời gian giữ ghế phải từ 1 đến 120 phút.")]
    [Display(Name = "Thời gian giữ ghế (phút)")]
    public int HoldMinutes { get; set; } = 15;

    public List<TierForm> Tiers { get; set; } = new();

    [Required(ErrorMessage = "Nhập tên công ty.")]
    [StringLength(100, ErrorMessage = "Tên công ty tối đa 100 ký tự.")]
    public string? CompanyName { get; set; }

    [StringLength(500, ErrorMessage = "Đường dẫn logo tối đa 500 ký tự.")]
    public string? LogoUrl { get; set; }

    [StringLength(30, ErrorMessage = "Hotline tối đa 30 ký tự.")]
    public string? Hotline { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string? Email { get; set; }
}
