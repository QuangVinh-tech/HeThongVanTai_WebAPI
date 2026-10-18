using System.Security.Cryptography;
using System.Text;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

public record IssuedRefreshToken(string Token, DateTime ExpiresAt);

/// <summary>
/// Cấp, xoay vòng và thu hồi refresh token. Mỗi lần làm mới, token cũ bị thu hồi và cấp token mới.
/// Token đã dùng mà bị đưa ra lần nữa (ngoài khoảng ân hạn) bị coi là nghi lộ: thu hồi mọi phiên của người đó.
/// </summary>
public class RefreshTokenService
{
    // Hai request song song có thể cùng mang một token cũ trong vài giây đầu: không coi là bị lộ
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(20);

    private readonly VanTaiAuthDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(VanTaiAuthDbContext db, IConfiguration config, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private int LifetimeDays => Math.Max(1, _config.GetValue("Jwt:RefreshTokenDays", 7));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<IssuedRefreshToken> IssueAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var token = NewToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(LifetimeDays)
        };
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();

        // Dọn token đã hết hạn từ lâu của người này để bảng không phình ra
        var old = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < now.AddDays(-30))
            .ToListAsync();
        if (old.Count > 0)
        {
            _db.RefreshTokens.RemoveRange(old);
            await _db.SaveChangesAsync();
        }

        return new IssuedRefreshToken(token, entity.ExpiresAt);
    }

    /// <summary>Đổi token cũ lấy token mới. Trả null nếu token không hợp lệ, đã thu hồi hoặc hết hạn.</summary>
    public async Task<(string UserId, IssuedRefreshToken Token)?> RotateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = Hash(token);
        var current = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (current == null) return null;

        var now = DateTime.UtcNow;
        if (current.RevokedAt != null)
        {
            var justRotated = current.ReplacedByHash != null && now - current.RevokedAt.Value < ReuseGrace;
            if (!justRotated)
            {
                _logger.LogWarning("Refresh token đã dùng bị đưa ra lần nữa, thu hồi mọi phiên của người dùng {UserId}", current.UserId);
                await RevokeAllAsync(current.UserId);
            }
            return null;
        }
        if (current.ExpiresAt <= now) return null;

        var issued = await IssueAsync(current.UserId);
        current.RevokedAt = now;
        current.ReplacedByHash = Hash(issued.Token);
        await _db.SaveChangesAsync();
        return (current.UserId, issued);
    }

    public async Task RevokeAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var hash = Hash(token);
        var current = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
        if (current == null) return;
        current.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>Thu hồi mọi phiên của người dùng (có thể giữ lại một token, ví dụ phiên đang đổi mật khẩu).</summary>
    public async Task<int> RevokeAllAsync(string userId, string? exceptToken = null)
    {
        var keep = string.IsNullOrWhiteSpace(exceptToken) ? null : Hash(exceptToken);
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        var count = 0;
        foreach (var t in tokens)
        {
            if (keep != null && t.TokenHash == keep) continue;
            t.RevokedAt = now;
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync();
        return count;
    }
}
