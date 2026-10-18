using HeThongVanTai.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HeThongVanTai.Services;

/// <summary>Kiểm tra kết nối tới một CSDL (dùng cho /health). Không đưa chi tiết lỗi ra ngoài.</summary>
public class DbHealthCheck<TContext> : IHealthCheck where TContext : DbContext
{
    private readonly TContext _db;

    public DbHealthCheck(TContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Chờ tối đa 5 giây để CSDL treo không làm treo luôn endpoint kiểm tra
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            return await _db.Database.CanConnectAsync(cts.Token)
                ? HealthCheckResult.Healthy("Kết nối được")
                : HealthCheckResult.Unhealthy("Không kết nối được CSDL");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Lỗi khi kiểm tra CSDL", ex);
        }
    }
}

/// <summary>Kết quả /health dạng JSON: trạng thái chung và từng thành phần. Không có chuỗi kết nối hay nội dung lỗi.</summary>
public static class HealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                ms = (int)e.Value.Duration.TotalMilliseconds
            })
        };
        return context.Response.WriteAsJsonAsync(body);
    }
}
