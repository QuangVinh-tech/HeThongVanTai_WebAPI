using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Services.Audit;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, object? entityId, string? detail = null);
}

/// <summary>
/// Ghi nhật ký thao tác. Lỗi khi ghi nhật ký chỉ được log lại, KHÔNG làm hỏng thao tác chính.
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, object? entityId, string? detail = null)
    {
        try
        {
            var user = _http.HttpContext?.User;
            var row = new AuditLog
            {
                CreatedAt = DateTime.Now,
                UserId = user == null ? null : UserClaims.UserId(user),
                UserName = Truncate((user == null ? null : UserClaims.Email(user)) ?? "hệ thống", 256),
                Action = Truncate(action, 50),
                EntityType = Truncate(entityType, 50),
                EntityId = entityId?.ToString() is { } id ? Truncate(id, 50) : null,
                Detail = string.IsNullOrWhiteSpace(detail) ? null : Truncate(detail.Replace('\r', ' ').Replace('\n', ' '), 1000)
            };
            _db.AuditLogs.Add(row);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký thao tác {Action} {Entity}", action, entityType);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

public static class AuditExtensions
{
    // Cho phép controller được tạo không có audit (ví dụ trong test): gọi trên null thì bỏ qua
    public static Task AuditAsync(this IAuditService? audit, string action, string entityType, object? entityId, string? detail = null)
        => audit == null ? Task.CompletedTask : audit.LogAsync(action, entityType, entityId, detail);
}
