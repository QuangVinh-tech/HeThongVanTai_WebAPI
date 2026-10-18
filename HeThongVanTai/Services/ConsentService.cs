using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

/// <summary>Ghi nhận và tra cứu việc người dùng đồng ý Điều khoản sử dụng và Chính sách bảo mật.</summary>
public class ConsentService
{
    public const string DefaultVersion = "1.0";
    public const int MaxVersionLength = 20;

    private readonly VanTaiAuthDbContext _db;

    public ConsentService(VanTaiAuthDbContext db) => _db = db;

    public static string NormalizeVersion(string? version)
    {
        var v = (version ?? "").Trim();
        if (v.Length == 0) return DefaultVersion;
        return v.Length > MaxVersionLength ? v[..MaxVersionLength] : v;
    }

    /// <summary>Ghi nhận đồng ý. Cùng người dùng, cùng phiên bản thì chỉ ghi một lần (giữ ngày đồng ý đầu tiên).</summary>
    public async Task RecordAsync(string userId, string? version)
    {
        var v = NormalizeVersion(version);
        if (await _db.UserConsents.AnyAsync(c => c.UserId == userId && c.TermsVersion == v)) return;

        _db.UserConsents.Add(new UserConsent { UserId = userId, TermsVersion = v, AcceptedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public Task<UserConsent?> LatestAsync(string userId) =>
        _db.UserConsents.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AcceptedAt)
            .FirstOrDefaultAsync();
}
