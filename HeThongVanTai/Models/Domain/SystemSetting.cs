using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Models.Domain;

/// <summary>Cài đặt hệ thống dạng khóa - giá trị (bảng SystemSettings).</summary>
public class SystemSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = null!;

    [Required, MaxLength(4000)]
    public string Value { get; set; } = "";

    public DateTime UpdatedAt { get; set; }

    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
