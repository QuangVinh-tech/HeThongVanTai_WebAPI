using System.Text.Json;
using System.Text.RegularExpressions;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

/// <summary>
/// Đọc và lưu cài đặt hệ thống (thời gian giữ ghế, mốc hoàn tiền, thông tin công ty).
/// Không cache: lưu xong có hiệu lực ngay với lần đặt vé và hủy vé tiếp theo.
/// </summary>
public class SettingsService
{
    public const string KeyHold = "Booking.HoldMinutes";
    public const string KeyTiers = "Refund.Tiers";
    public const string KeyCompany = "Company.Name";
    public const string KeyLogo = "Company.LogoUrl";
    public const string KeyHotline = "Company.Hotline";
    public const string KeyEmail = "Company.Email";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex HotlineRx = new(@"^[0-9 +().\-]{0,30}$");
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public SettingsService(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public SettingsDto Defaults() => new()
    {
        HoldMinutes = _cfg.GetValue<int?>("Booking:HoldMinutes") ?? 15,
        RefundTiers = Rules.DefaultRefundTiers
            .Select(t => new RefundTierDto { HoursBefore = t.HoursBefore, RatePercent = t.RatePercent }).ToList(),
        CompanyName = "VậnTải HK",
        LogoUrl = "",
        Hotline = "",
        Email = ""
    };

    public async Task<SettingsDto> GetAsync()
    {
        var s = Defaults();
        var rows = await _db.SystemSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value);

        if (rows.TryGetValue(KeyHold, out var hold) && int.TryParse(hold, out var minutes) && minutes is >= 1 and <= 120)
            s.HoldMinutes = minutes;

        if (rows.TryGetValue(KeyTiers, out var json))
        {
            try
            {
                var tiers = JsonSerializer.Deserialize<List<RefundTierDto>>(json, Json);
                if (tiers != null && tiers.Count > 0 && ValidateTiers(tiers) == null)
                    s.RefundTiers = tiers.OrderByDescending(t => t.HoursBefore).ToList();
            }
            catch (JsonException) { /* dữ liệu hỏng thì dùng mặc định */ }
        }

        if (rows.TryGetValue(KeyCompany, out var name) && !string.IsNullOrWhiteSpace(name)) s.CompanyName = name;
        if (rows.TryGetValue(KeyLogo, out var logo)) s.LogoUrl = logo;
        if (rows.TryGetValue(KeyHotline, out var hotline)) s.Hotline = hotline;
        if (rows.TryGetValue(KeyEmail, out var email)) s.Email = email;
        return s;
    }

    /// <summary>Trả về thông báo lỗi tiếng Việt, hoặc null nếu hợp lệ.</summary>
    public static string? Validate(SettingsDto? dto)
    {
        if (dto == null) return "Thiếu dữ liệu cài đặt.";
        if (dto.HoldMinutes < 1 || dto.HoldMinutes > 120) return "Thời gian giữ ghế phải từ 1 đến 120 phút.";

        var tierError = ValidateTiers(dto.RefundTiers);
        if (tierError != null) return tierError;

        var name = (dto.CompanyName ?? "").Trim();
        if (name.Length == 0) return "Tên công ty không được để trống.";
        if (name.Length > 100) return "Tên công ty tối đa 100 ký tự.";

        var logo = (dto.LogoUrl ?? "").Trim();
        if (logo.Length > 500) return "Đường dẫn logo tối đa 500 ký tự.";
        if (logo.Length > 0)
        {
            var okRelative = logo.StartsWith("/") && !logo.StartsWith("//");
            var okAbsolute = Uri.TryCreate(logo, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https");
            if (!okRelative && !okAbsolute) return "Logo phải là đường dẫn http(s) hoặc bắt đầu bằng dấu /.";
        }

        var hotline = (dto.Hotline ?? "").Trim();
        if (!HotlineRx.IsMatch(hotline)) return "Hotline chỉ gồm số, khoảng trắng và + ( ) . - (tối đa 30 ký tự).";

        var email = (dto.Email ?? "").Trim();
        if (email.Length > 256 || (email.Length > 0 && !EmailRx.IsMatch(email))) return "Email không hợp lệ.";
        return null;
    }

    public static string? ValidateTiers(List<RefundTierDto>? tiers)
    {
        if (tiers == null || tiers.Count < 1 || tiers.Count > 10) return "Cần từ 1 đến 10 mốc hoàn tiền.";
        foreach (var t in tiers)
        {
            if (t.HoursBefore < 0 || t.HoursBefore > 720) return "Số giờ của mốc hoàn tiền phải từ 0 đến 720.";
            if (t.RatePercent < 0 || t.RatePercent > 100) return "Tỉ lệ hoàn tiền phải từ 0 đến 100%.";
        }
        if (tiers.Select(t => t.HoursBefore).Distinct().Count() != tiers.Count)
            return "Các mốc hoàn tiền không được trùng số giờ.";

        var sorted = tiers.OrderByDescending(t => t.HoursBefore).ToList();
        for (var i = 1; i < sorted.Count; i++)
            if (sorted[i].RatePercent > sorted[i - 1].RatePercent)
                return "Hủy càng sát giờ chạy thì tỉ lệ hoàn không được cao hơn.";
        return null;
    }

    /// <summary>Lưu cài đặt. Trả về thông báo lỗi nếu dữ liệu sai, null nếu đã lưu.</summary>
    public async Task<string?> SaveAsync(SettingsDto dto, string? updatedBy)
    {
        var error = Validate(dto);
        if (error != null) return error;

        var values = new Dictionary<string, string>
        {
            [KeyHold] = dto.HoldMinutes.ToString(),
            [KeyTiers] = JsonSerializer.Serialize(
                dto.RefundTiers.OrderByDescending(t => t.HoursBefore)
                    .Select(t => new RefundTierDto { HoursBefore = t.HoursBefore, RatePercent = t.RatePercent }).ToList(), Json),
            [KeyCompany] = (dto.CompanyName ?? "").Trim(),
            [KeyLogo] = (dto.LogoUrl ?? "").Trim(),
            [KeyHotline] = (dto.Hotline ?? "").Trim(),
            [KeyEmail] = (dto.Email ?? "").Trim()
        };

        var keys = values.Keys.ToList();
        var existing = await _db.SystemSettings.Where(x => keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key);
        var now = DateTime.Now;
        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = now;
                row.UpdatedBy = updatedBy;
            }
            else
            {
                _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = now, UpdatedBy = updatedBy });
            }
        }
        await _db.SaveChangesAsync();
        return null;
    }
}
