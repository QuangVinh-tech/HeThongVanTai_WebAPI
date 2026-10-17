namespace HeThongVanTai.Models.DTO;

/// <summary>Một mốc hoàn tiền: hủy khi còn từ HoursBefore giờ trở lên thì hoàn RatePercent %.</summary>
public class RefundTierDto
{
    public int HoursBefore { get; set; }
    public int RatePercent { get; set; }
}

public class SettingsDto
{
    public int HoldMinutes { get; set; } = 15;
    public List<RefundTierDto> RefundTiers { get; set; } = new();
    public string? CompanyName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Hotline { get; set; }
    public string? Email { get; set; }
}
